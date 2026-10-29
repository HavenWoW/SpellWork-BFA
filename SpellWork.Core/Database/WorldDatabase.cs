using MySql.Data.MySqlClient;
using SpellWork.Extensions;
using System;
using System.Collections.Generic;

namespace SpellWork.Database
{
    public class WorldDatabase
    {
        private readonly SpellWorkConfig _config;

        public WorldDatabase(SpellWorkConfig config)
        {
            _config = config;
        }

        public bool Connected { get; private set; }
        public List<string> Dropped { get; } = new List<string>();

        private string ConnectionString
        {
            get
            {
                if (_config.MySqlHost == ".")
                    return
                        $"Server=localhost;Pipe={_config.MySqlPortOrPipe};UserID={_config.MySqlUser};Password={_config.MySqlPassword};Database={_config.WorldDatabase};CharacterSet=utf8;ConnectionTimeout=5;ConnectionProtocol=Pipe;";

                return
                    $"Server={_config.MySqlHost};Port={_config.MySqlPortOrPipe};UserID={_config.MySqlUser};Password={_config.MySqlPassword};Database={_config.WorldDatabase};CharacterSet=utf8;ConnectionTimeout=5;";
            }
        }

        private string GetSpellName(uint id)
        {
            if (DBC.DBC.SpellInfoStore.ContainsKey((int)id))
                return DBC.DBC.SpellInfoStore[(int)id].Name;

            Dropped.Add($"DELETE FROM `spell_proc_event` WHERE `entry` IN ({id.ToUInt32()});\r\n");
            return string.Empty;
        }

        /// <summary>spell_proc_event.phase is a MySQL SET
        /// ('PROC_PHASE_HIT','PROC_PHASE_CAST','PROC_PHASE_AFTER_CAST'), which
        /// the driver returns as a comma-separated string; the bitmask is the
        /// 1-based member position (HIT=1, CAST=2, AFTER_CAST=4). Numeric
        /// values (e.g. from phase+0) pass through.</summary>
        public static uint ReadPhase(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0;
            if (value is string s)
            {
                uint mask = 0;
                foreach (var part in s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    switch (part.Trim())
                    {
                        case "PROC_PHASE_HIT": mask |= 1; break;
                        case "PROC_PHASE_CAST": mask |= 2; break;
                        case "PROC_PHASE_AFTER_CAST": mask |= 4; break;
                        default:
                            if (uint.TryParse(part.Trim(), out var n)) mask |= n;
                            break;
                    }
                }
                return mask;
            }
            return Convert.ToUInt32(value);
        }

        public List<SpellProcEventEntry> SelectProc(string query)
        {
            var result = new List<SpellProcEventEntry>();

            using (var connection = new MySql.Data.MySqlClient.MySqlConnection(ConnectionString))
            {
                var command = new MySqlCommand(query, connection);
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var spellId = reader.GetUInt32(0);
                        result.Add(new SpellProcEventEntry
                        {
                            Id                  = spellId,
                            SpellName           = GetSpellName(spellId),
                            SchoolMask          = reader.GetByte(1),
                            SpellFamilyName     = reader.GetUInt16(2),
                            SpellFamilyMask     = new[]
                            {
                                reader.GetUInt32(3),
                                reader.GetUInt32(4),
                                reader.GetUInt32(5),
                                reader.GetUInt32(6)
                            },
                            ProcFlags           = reader.GetUInt32(7),
                            ProcEx              = reader.GetUInt32(8),
                            Phase               = ReadPhase(reader.GetValue(9)),
                            PpmRate             = reader.GetFloat(10),
                            CustomChance        = reader.GetFloat(11),
                            Cooldown            = reader.GetUInt32(12)
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Wall-clock ceiling for one <see cref="SelectRows"/> statement, in
        /// seconds. MySql.Data's default is 30 and inheriting it silently is
        /// not a decision; this is. Fifteen seconds is generous for anything
        /// this server is for -- indexed lookups on the spell tables answer in
        /// milliseconds, and a full scan of TrinityCore's largest world tables
        /// is still well inside it -- while bounding what a single
        /// agent-composed query can hold a connection open for. The row limit
        /// does not bound that: the server does the work before the reader
        /// stops taking rows. This is the backstop for the blocking construct
        /// SqlGuard's keyword list has not thought of yet, not just for the
        /// ones it has.
        /// </summary>
        public const int SelectRowsTimeoutSeconds = 15;

        public sealed class QueryResult
        {
            public List<string> Columns { get; } = new List<string>();
            public List<Dictionary<string, object>> Rows { get; } = new List<Dictionary<string, object>>();
            public bool Truncated { get; set; }
        }

        /// <summary>
        /// Runs a caller-supplied SELECT and returns up to <paramref name="limit"/> rows.
        /// The query text is not parameterisable — it is composed by the
        /// caller — which is why SpellWork.Mcp.SqlGuard validates it first and
        /// why the documented MySQL account is a read-only grant. Neither
        /// control is sufficient alone.
        /// </summary>
        public QueryResult SelectRows(string sql, int limit, params (string Name, object Value)[] parameters)
        {
            var result = new QueryResult();

            using (var connection = new MySqlConnection(ConnectionString))
            using (var command = new MySqlCommand(sql, connection))
            {
                // Callers that build the statement themselves bind values here
                // (@name), never by concatenation.
                foreach (var (name, value) in parameters)
                    command.Parameters.AddWithValue(name, value);

                command.CommandTimeout = SelectRowsTimeoutSeconds;
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    // A join can return the same column name twice
                    // (spell_proc.entry and spell_threat.entry, say). Rows are
                    // dictionaries, so a repeat would overwrite the earlier
                    // value and hand back a row that quietly lost a field;
                    // suffixing keeps every column addressable and keeps
                    // Columns and the row keys in agreement.
                    for (var i = 0; i < reader.FieldCount; i++)
                        result.Columns.Add(UniqueColumnName(result.Columns, reader.GetName(i)));

                    while (reader.Read())
                    {
                        if (result.Rows.Count >= limit)
                        {
                            result.Truncated = true;
                            break;
                        }

                        var row = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var value = reader.GetValue(i);
                            row[result.Columns[i]] = value == DBNull.Value ? null : value;
                        }

                        result.Rows.Add(row);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// item_template names by entry, for reagent names (3.3.5 Item.dbc
        /// has none). Ids are bound as parameters; an id with no row is
        /// simply absent from the result.
        /// </summary>
        public Dictionary<int, string> SelectItemNames(IEnumerable<int> entries)
        {
            var ids = new List<int>(new HashSet<int>(entries ?? new int[0]));
            ids.RemoveAll(id => id <= 0);

            var names = new Dictionary<int, string>();
            if (ids.Count == 0)
                return names;

            var parameters = new (string Name, object Value)[ids.Count];
            var placeholders = new string[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                placeholders[i] = "@e" + i;
                parameters[i] = (placeholders[i], ids[i]);
            }

            var result = SelectRows(
                "SELECT `entry`, `name` FROM `item_template` WHERE `entry` IN (" + string.Join(", ", placeholders) + ")",
                ids.Count, parameters);

            foreach (var row in result.Rows)
                names[Convert.ToInt32(row["entry"])] = row["name"] as string;

            return names;
        }

        private static string UniqueColumnName(List<string> taken, string name)
        {
            if (string.IsNullOrEmpty(name))
                name = "column";

            if (!taken.Contains(name))
                return name;

            for (var suffix = 2; ; suffix++)
            {
                var candidate = name + "_" + suffix;
                if (!taken.Contains(candidate))
                    return candidate;
            }
        }

        public void Insert(string query)
        {
            using (var connection = new MySql.Data.MySqlClient.MySqlConnection(ConnectionString))
            {
                var command = new MySqlCommand(query, connection);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        public bool TestConnection(out string error)
        {
            error = null;

            if (!_config.UseDatabase)
            {
                Connected = false;
                return false;
            }

            try
            {
                using (var connection = new MySql.Data.MySqlClient.MySqlConnection(ConnectionString))
                {
                    connection.Open();
                }

                Connected = true;
                return true;
            }
            catch (MySqlException ex)
            {
                error = $"Errno {ex.Number}{Environment.NewLine}{ex.Message}";
                Connected = false;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Connected = false;
                return false;
            }
        }
    }
}
