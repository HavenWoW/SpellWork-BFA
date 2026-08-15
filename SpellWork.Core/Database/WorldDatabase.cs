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
                                reader.GetUInt32(5)
                            },
                            ProcFlags           = reader.GetUInt32(6),
                            ProcEx              = reader.GetUInt32(7),
                            PpmRate             = reader.GetFloat(8),
                            CustomChance        = reader.GetFloat(9),
                            Cooldown            = reader.GetUInt32(10)
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
        public QueryResult SelectRows(string sql, int limit)
        {
            var result = new QueryResult();

            using (var connection = new MySqlConnection(ConnectionString))
            using (var command = new MySqlCommand(sql, connection))
            {
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
