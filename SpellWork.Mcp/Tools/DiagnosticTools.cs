using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.Database;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class DiagnosticTools
    {
        private readonly SpellDataService _data;

        public DiagnosticTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "server_info")]
        [Description("Configured paths, locale, store counts, load duration and MySQL reachability. Call this first when something looks wrong.")]
        public async Task<string> ServerInfo()
        {
            var data = await _data.GetAsync();
            var config = _data.Config;

            var payload = new Dictionary<string, object>
            {
                ["version"] = SpellWork.DBC.DBC.Version,
                ["dbcPath"] = config.DbcPath,
                ["gtPath"] = config.GtPath,
                ["db2Path"] = config.Db2Path,
                ["locale"] = config.Locale,
                ["clientDataLoaded"] = data.Loaded,
                ["loadDurationMs"] = (int)data.LoadDuration.TotalMilliseconds,
                ["spellCount"] = data.SpellCount,
                // Only the non-empty stores. StoreCounts holds one entry per
                // Storage<T> property on DBC.DBC -- however many that is.
                // CountStores discovers them by reflection, so naming a figure
                // here would only go stale the next time a store is added or
                // removed. On a misconfigured server most of those entries are
                // zero, and a zero says exactly what an absence says.
                ["storeCounts"] = data.StoreCounts.Where(pair => pair.Value > 0)
                    .OrderBy(pair => pair.Key)
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                ["mysqlConfigured"] = config.MySqlConfigured,
            };

            if (!data.Loaded && data.ErrorCode != null)
            {
                payload["clientDataError"] = new Dictionary<string, object>
                {
                    ["code"] = data.ErrorCode,
                    ["message"] = data.ErrorMessage,
                    ["variable"] = data.ErrorVariable,
                };
            }

            // Only the tables LayoutVerification.Expected actually checks, and
            // only when at least one disagrees with the client build this
            // structure class was written against (DBC.cs's LayoutMismatches
            // is empty, never null, on a matching build). An agent seeing
            // garbled visual data from get_spell_visual_chain needs one place
            // that says the client build does not match, rather than having
            // to suspect every field of every table individually.
            var layoutMismatches = SpellWork.DBC.DBC.LayoutMismatches
                .Select(mismatch => (object)mismatch.Describe())
                .ToList();
            if (layoutMismatches.Count > 0)
                payload["layoutMismatches"] = layoutMismatches;

            if (config.MySqlConfigured)
            {
                var database = new WorldDatabase(config.ToSpellWorkConfig());
                payload["mysqlReachable"] = database.TestConnection(out var error);
                if (error != null)
                    payload["mysqlError"] = error;
            }

            return ToolResponse.Ok(payload);
        }
    }
}
