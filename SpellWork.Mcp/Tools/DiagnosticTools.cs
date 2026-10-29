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
        // The client build every structure class was written against.
        private const int ClientBuild = 18414;

        private readonly SpellDataService _data;

        public DiagnosticTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "server_info")]
        [Description("Configured paths, client build, locale (and whether the root was used instead), the file each store was opened from, layout mismatches, store counts, load duration and MySQL reachability. Call this first when something looks wrong.")]
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
                ["clientBuild"] = ClientBuild,
                ["localeSlot"] = SpellWork.DBC.DBC.LocaleSlot,
                ["localeSlotFallback"] = SpellWork.DBC.DBC.LocaleSlotFallback,
                ["openedFiles"] = SpellWork.DBC.DBC.OpenedFiles
                    .OrderBy(pair => pair.Key, System.StringComparer.Ordinal)
                    .ToDictionary(pair => pair.Key, pair => (object)new Dictionary<string, object> { ["path"] = pair.Value.Path, ["extension"] = pair.Value.Extension }),
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

            // Always present: an empty array says the layouts match the client
            // build the structure classes were written against (18414), which is
            // an answer an agent chasing garbled visual data needs, where a
            // missing key would read as "not checked".
            var layoutMismatches = SpellWork.DBC.DBC.LayoutMismatches
                .Select(mismatch => (object)new Dictionary<string, object>
                {
                    ["table"] = mismatch.Table,
                    ["file"] = mismatch.File,
                    ["expectedFieldsCount"] = mismatch.ExpectedFieldsCount,
                    ["actualFieldsCount"] = mismatch.ActualFieldsCount,
                    ["expectedRecordSize"] = mismatch.ExpectedRecordSize,
                    ["actualRecordSize"] = mismatch.ActualRecordSize,
                    ["message"] = mismatch.Describe(),
                })
                .ToList();
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
