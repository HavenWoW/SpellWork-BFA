using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using MySql.Data.MySqlClient;
using SpellWork.Database;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class DatabaseTools
    {
        /// <summary>MySQL's ER_NO_SUCH_TABLE. The one failure that means
        /// "this schema does not have that table" rather than "something went
        /// wrong".</summary>
        internal const int MySqlErrorNoSuchTable = 1146;

        // Every table get_spell_db_overrides collects, with the column that
        // holds the spell id in each. TrinityCore names these inconsistently,
        // which is why the column travels with the table name rather than
        // being assumed.
        //
        // Not every fork has every one of these: spell_bonus_data and
        // spell_ranks were dropped from TrinityCore's BfA-era branch and are
        // absent from the world schema this server is primarily built against
        // (verified against BFA-HavenCore-Corruption/sql/base/bfa_world.sql,
        // where the other five exist with exactly these id columns). They stay
        // in the list because other forks still have them and the tool should
        // stay portable -- their absence is reported as an absence, not as a
        // failure. See GetSpellDbOverrides.
        private static readonly (string Table, string IdColumn)[] OverrideTables =
        {
            ("spell_proc", "SpellId"),
            ("spell_script_names", "spell_id"),
            ("spell_custom_attr", "entry"),
            ("spell_bonus_data", "entry"),
            ("spell_threat", "entry"),
            ("spell_ranks", "spell_id"),
            ("spell_dbc", "Id"),
        };

        private readonly SpellDataService _data;

        public DatabaseTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "query_world_db")]
        [Description("Run one read-only SELECT against the configured world database. Multiple statements, DDL and DML are rejected. The server should be configured with a read-only MySQL user.")]
        public Task<string> QueryWorldDb(string sql, string database = "world", int limit = 100)
        {
            // Validate first, unconditionally: a malformed query is a caller
            // error whether or not a database happens to be reachable, and
            // rejecting before any connection is attempted is what makes this
            // testable with no server present.
            if (!SqlGuard.TryValidate(sql, out var rejection))
                return Task.FromResult(ToolResponse.Error("sql_rejected", rejection));

            var config = _data.Config;
            if (!config.MySqlConfigured)
                return Task.FromResult(NotConfigured());

            if (!string.Equals(database, config.WorldDatabase, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ToolResponse.Error("unknown_database",
                    $"This server is configured for database '{config.WorldDatabase}'. " +
                    "Set SPELLWORK_MYSQL_WORLD_DB to query a different one.",
                    new Dictionary<string, object> { ["configuredDatabase"] = config.WorldDatabase }));
            }

            var effectiveLimit = EffectiveLimit(limit);

            try
            {
                var worldDatabase = new WorldDatabase(config.ToSpellWorkConfig());
                var result = worldDatabase.SelectRows(sql, effectiveLimit);

                return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
                {
                    ["database"] = config.WorldDatabase,
                    ["columns"] = result.Columns,
                    ["rows"] = result.Rows.Cast<object>().ToList(),
                    ["count"] = result.Rows.Count,
                    ["limit"] = effectiveLimit,
                    ["truncated"] = result.Truncated,
                    ["maxRows"] = ToolResponse.MaxRows,
                }));
            }
            catch (Exception ex)
            {
                // The driver's message names the syntax error or the missing
                // table, which is what the agent needs to fix its query.
                return Task.FromResult(ToolResponse.Error("query_failed", ex.Message));
            }
        }

        [McpServerTool(Name = "get_spell_db_overrides")]
        [Description("Rows for one spell id from whichever TrinityCore spell override tables this world database has: spell_proc, spell_script_names, spell_custom_attr, spell_bonus_data, spell_threat, spell_ranks, spell_dbc. Forks differ in which exist; tables the schema does not have are listed under missingTables, which is expected and not an error.")]
        public Task<string> GetSpellDbOverrides(int id)
        {
            var config = _data.Config;
            if (!config.MySqlConfigured)
                return Task.FromResult(NotConfigured());

            var worldDatabase = new WorldDatabase(config.ToSpellWorkConfig());

            // Ask once whether the server is reachable at all. Without this a
            // server that is simply down reports the same connection error
            // seven times, once per table, and the caller has to read all
            // seven to work out that none of them is about a table.
            if (!worldDatabase.TestConnection(out var connectionError))
            {
                return Task.FromResult(ToolResponse.Error("connection_failed",
                    $"Could not connect to '{config.WorldDatabase}' on {config.MySqlHost}: {connectionError}"));
            }

            var tables = new Dictionary<string, object>();
            var missing = new List<string>();
            var truncated = new List<string>();
            var errors = new Dictionary<string, object>();

            foreach (var (table, idColumn) in OverrideTables)
            {
                // id is an int parameter, not caller-supplied text, so this
                // interpolation cannot carry an injection. Everything else in
                // the statement is a compile-time constant. Note what this
                // does NOT do: run the string through SqlGuard and call it
                // safe. The guard is for text an agent composed; what makes
                // this safe is that id is an int.
                var sql = $"SELECT * FROM `{table}` WHERE `{idColumn}` = {id}";

                try
                {
                    var result = worldDatabase.SelectRows(sql, ToolResponse.MaxRows);
                    tables[table] = result.Rows.Cast<object>().ToList();
                    if (result.Truncated)
                        truncated.Add(table);
                }
                catch (Exception ex) when (IsMissingTable(ex))
                {
                    // Expected, not a failure: two of these seven tables are
                    // absent from TrinityCore's BfA-era world schema, so this
                    // branch fires on every call against it. Reporting an
                    // expected absence in the same shape as a real fault --
                    // a syntax error, a permission denial, a wrong id column
                    // -- would train the caller to ignore both.
                    missing.Add(table);
                }
                catch (Exception ex)
                {
                    errors[table] = ex.Message;
                }
            }

            var payload = new Dictionary<string, object>
            {
                ["id"] = id,
                ["database"] = config.WorldDatabase,
                ["tables"] = tables,
            };

            if (missing.Count > 0)
            {
                payload["missingTables"] = missing;
                payload["missingTablesNote"] =
                    "These tables do not exist in this schema (MySQL error 1146). Forks differ in which " +
                    "override tables they keep; this is not a failure.";
            }

            if (truncated.Count > 0)
                payload["truncatedTables"] = truncated;

            if (errors.Count > 0)
                payload["unavailableTables"] = errors;

            return Task.FromResult(ToolResponse.Ok(payload));
        }

        /// <summary>
        /// The row ceiling this call actually applied, which is also the value
        /// the response echoes as "limit".
        /// <para>
        /// No lower floor on the rows returned: ToolResponse.Paged is the
        /// authoritative clamp and its contract says a non-positive limit
        /// yields zero rows, never one and never "unbounded". A second,
        /// disagreeing clamp here is exactly the drift Paged was made
        /// authoritative to prevent. What the negative branch fixes is the
        /// *echo*: Math.Min(-5, MaxRows) reports -5 back to a caller that got
        /// zero rows, while Paged -- which the other twelve paging tools go
        /// through -- sanitises the same input to 0 and documents that the
        /// reported limit is the effective applied ceiling. Thirteen tools
        /// must not disagree about what "limit" means. Passing 0 rather than
        /// -5 to SelectRows returns the same zero rows either way, so this
        /// changes no result set.
        /// </para>
        /// </summary>
        internal static int EffectiveLimit(int limit) =>
            Math.Min(limit < 0 ? 0 : limit, ToolResponse.MaxRows);

        /// <summary>
        /// True only for MySQL's "table does not exist". Deliberately keyed on
        /// the driver's error number rather than on the message text: the
        /// message is localised and reworded between server versions, and a
        /// text match that drifted would start filing real faults -- a
        /// permission denial, a syntax error -- as "this table is simply not
        /// here", which is the one mistake this split exists to prevent.
        /// </summary>
        internal static bool IsMissingTable(Exception ex) =>
            ex is MySqlException mysql && mysql.Number == MySqlErrorNoSuchTable;

        private static string NotConfigured() =>
            ToolResponse.Error("mysql_not_configured",
                "No MySQL connection is configured. Set SPELLWORK_MYSQL_HOST, SPELLWORK_MYSQL_USER and " +
                "SPELLWORK_MYSQL_WORLD_DB (and SPELLWORK_MYSQL_PASS / SPELLWORK_MYSQL_PORT if needed).",
                new Dictionary<string, object>
                {
                    ["variables"] = new[]
                    {
                        "SPELLWORK_MYSQL_HOST", "SPELLWORK_MYSQL_PORT", "SPELLWORK_MYSQL_USER",
                        "SPELLWORK_MYSQL_PASS", "SPELLWORK_MYSQL_WORLD_DB",
                    },
                });
    }
}
