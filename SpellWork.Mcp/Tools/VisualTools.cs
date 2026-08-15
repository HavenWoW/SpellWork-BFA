using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.Spell;

namespace SpellWork.Mcp.Tools
{
    /// <summary>
    /// The fourteenth tool: exposes <see cref="SpellVisualChainResolver.Resolve"/>
    /// over MCP. No new logic of its own beyond projecting the resolver's POCOs
    /// to plain dictionaries -- the WinForms renderer (a later task) consumes
    /// the same resolver so the two cannot drift apart.
    /// <para>
    /// One gate, not the other tool classes' two branches -- but it folds in
    /// the same checks, it does not drop one. Every other tool in this
    /// namespace awaits <c>_data.GetAsync()</c>, branches on <c>data.Loaded</c>
    /// (routing to <c>DataError(data)</c>, which surfaces <c>data.ErrorCode</c>/
    /// <c>ErrorMessage</c>/<c>ErrorVariable</c>) as its own first check, then
    /// separately null-checks the specific store(s) it needs. A single
    /// combined condition -- <c>!data.Loaded || section.Unavailable</c> -- is
    /// used here instead, reported as one <c>store_not_loaded</c> message
    /// rather than two branches that would otherwise say almost the same thing
    /// (the message still distinguishes the two states; see the branch itself).
    /// <c>!data.Loaded</c> is load-bearing, not redundant:
    /// <c>DBC.DBC.LoadAsync</c> loads each <c>Storage&lt;T&gt;</c>
    /// independently, so a client-data directory missing some unrelated .db2
    /// (e.g. <c>SpellName.db2</c>) while still carrying every store this walk
    /// needs would leave them all non-null while <c>SpellInfoStore</c> never
    /// got built -- without the <c>!data.Loaded</c> term this gate would not
    /// trip, and every id would fall through to <c>NotFound(id)</c> against an
    /// empty store, misreporting a genuine load failure as an ordinary bad-id
    /// error. See <c>VisualToolsUnloadedStoreTests</c> for why checking this
    /// combined condition before the spell-id check -- rather than in a
    /// separate earlier branch -- is what makes the guard provable by a plain,
    /// unskippable test.
    /// </para>
    /// <para>
    /// <c>section.Unavailable</c> is <see cref="SpellVisualChainResolver"/>'s
    /// own answer, not a null check written here. Which stores this walk cannot
    /// do without is the resolver's question: hand-rolling it meant this tool
    /// once answered <c>ok:true</c> with an empty variant list --
    /// indistinguishable from "this spell has no visuals" -- for a missing
    /// <c>SpellXSpellVisual.db2</c> that <c>get_spell</c> correctly reported as
    /// <c>storeUnloaded</c>.
    /// </para>
    /// </summary>
    [McpServerToolType]
    public sealed class VisualTools
    {
        private readonly SpellDataService _data;

        public VisualTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "get_spell_visual_chain")]
        [Description("The resolved visual chain for one spell: per-difficulty/priority variants from " +
            "SpellXSpellVisual, each variant's missile info, and the SpellVisualKit rows it reaches. Kits are " +
            "reached from two directions, not three: the visual's own MissileTargetingKit ('visual') and its " +
            "SpellVisualEvent rows ('event'); SpellVisualMissile carries no kit reference at all -- its only " +
            "'Kit'-named field points at the unrelated AnimKit table -- so reachedVia is always exactly one of " +
            "'visual' or 'event'. A kit reached by both directions on the same variant is reported once; the same " +
            "kit id can still appear on more than one variant. Each kit's effects are raw SpellVisualKitEffect " +
            "rows: effectType and effect are uninterpreted ids whose meaning depends on effectType -- only " +
            "effectType 2 has a confirmed target (SpellVisualKitModelAttach) in this build's data, every other " +
            "value is unresolved. spellVisualMissileId is a SpellVisualMissile row id, NOT a SpellMissile id -- " +
            "the standalone SpellMissile table is keyed by spell and is not reachable from this chain. " +
            "IMPORTANT -- difficulty here is a raw SpellXSpellVisual.DifficultyID, NOT a MapDifficulty row id: " +
            "this is the one argument named 'difficulty' on this server that is not a MapDifficulty row id, and " +
            "the two id spaces do not coincide (SpellXSpellVisual.DifficultyID is a byte, so no MapDifficulty " +
            "row id of 256 or more can ever match a row here). Feed back the value get_spell reports as " +
            "visual.difficulty, or a value listed in variantsAtOtherDifficulties, rather than the difficulty " +
            "argument you passed to get_spell. Pass " +
            "difficulty -1 for every difficulty (0 is a real difficulty id, not a wildcard). When the filter " +
            "empties the list but the spell has variants elsewhere, variantsAtOtherDifficulties names those " +
            "difficulties -- an empty variants array with no such key means the spell has no SpellXSpellVisual " +
            "rows at all. Kits are capped at " +
            "500 across the whole response, not per variant; truncated is true when that cap withheld one. " +
            "unresolvedReferences lists ids this walk could not resolve (e.g. a SpellVisualID or " +
            "SpellVisualMissileSetID with no matching row); the key is omitted entirely, not emitted empty, " +
            "when there is nothing to report.")]
        public async Task<string> GetSpellVisualChain(int id, int difficulty = 0)
        {
            // Triggers SpellDataService's one-time client-data load if it has
            // not run yet. See the class remarks for why the combined
            // condition below (data.Loaded folded in, not dropped) is the
            // one gate this tool needs.
            var data = await _data.GetAsync();

            // One call answers all three questions this tool has to get right --
            // is the data available, what is the chain, and which difficulties
            // does the spell actually have variants at -- and it is the same
            // call get_spell's json and text sections make. See
            // SpellVisualSection for the divergences that came of each host
            // assembling its own answer from the parts.
            //
            // Note the difficulty argument goes in raw: unlike every other
            // 'difficulty' on this server it is a SpellXSpellVisual.DifficultyID,
            // not a MapDifficulty row id (see this method's Description).
            var section = SpellVisualChainResolver.ResolveSection(id, difficulty, ToolResponse.MaxRows);
            if (!data.Loaded || section.Unavailable)
            {
                // !data.Loaded is reported first and on its own terms. When no
                // client data loaded at all, every store is null and
                // MissingStores names all six -- naming those files there would
                // send an operator looking for files that are sitting on disk,
                // when the actual fault is the path, the locale or a load that
                // never ran. Only a load that did run, and left specific stores
                // behind, gets an inventory.
                var detail = !data.Loaded
                    ? "no client data is loaded at all, so none of them is available -- this is not a report " +
                      "that those six files are individually missing."
                    : "these did not load: " + string.Join(", ", section.MissingStores.Select(s => s + ".db2")) + ".";

                return ToolResponse.Error("store_not_loaded",
                    "No visual chain can be resolved -- it needs SpellXSpellVisual, SpellVisual, " +
                    "SpellVisualEvent, SpellVisualMissile, SpellVisualKit and SpellVisualKitEffect, and " +
                    detail + " Call server_info to see which stores are missing and why.");
            }

            if (!SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(id))
                return SpellTools.NotFound(id);

            var chain = section.Chain;

            var result = new Dictionary<string, object>
            {
                ["id"] = chain.SpellID,
                ["difficulty"] = difficulty,
                ["variants"] = chain.Variants.Select(SpellVisualChainJson.Describe).ToList(),
                ["truncated"] = chain.Truncated,
                ["maxKits"] = ToolResponse.MaxRows,
            };

            // The same key get_spell's visual section emits, for the same
            // reason: an empty variants list caused by the difficulty filter
            // must not read as "this spell has no visuals". This tool shipped
            // without it and returned exactly that bare empty list -- the
            // failure the JSON section had already been fixed for.
            if (section.DifficultiesWithVariants != null && section.DifficultiesWithVariants.Count > 0)
                result["variantsAtOtherDifficulties"] = section.DifficultiesWithVariants;

            AddIfNotEmpty(result, "unresolvedReferences", chain.UnresolvedReferences);

            return ToolResponse.Ok(result);
        }

        // The per-node projection (variant/kit/effect/missile, with their key
        // names, key order and the reasoning behind both) moved to
        // SpellVisualChainJson in SpellWork.Core in Task 7, where
        // SpellInfoJsonSerializer's "visual" section can reach it too. Core
        // cannot reference this assembly, so leaving it here would have meant
        // a second, independent projection of the same POCO graph -- free to
        // drift from this one. The envelope above stays here: maxKits is
        // ToolResponse.MaxRows, which is Mcp-only.

        // Mirrors SpellInfoJsonSerializer's AddIfNotEmpty (SpellWork.Core):
        // that helper is private to a different assembly, so this is a local
        // copy of the same shape/name rather than a bespoke inline check.
        private static void AddIfNotEmpty(IDictionary<string, object> dict, string key, IReadOnlyCollection<string> value)
        {
            if (value != null && value.Count > 0)
                dict[key] = value;
        }
    }
}
