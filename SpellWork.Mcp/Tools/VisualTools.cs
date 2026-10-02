using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.Spell;

namespace SpellWork.Mcp.Tools
{
    /// <summary>
    /// get_spell_visual_chain: the 5.4.8 chain (spec §6). Answers from the same
    /// <see cref="SpellVisualChainResolver.ResolveSection(int,int)"/> call that
    /// get_spell's JSON "visual" section and text block use.
    /// </summary>
    [McpServerToolType]
    public sealed class VisualTools
    {
        private readonly SpellDataService _data;

        public VisualTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "get_spell_visual_chain")]
        [Description("The resolved 5.4.8 visual chain for one spell. Takes only id: visuals are not per difficulty. " +
            "Variants come from SpellMisc.SpellVisualID[0] and [1] (difficulty-0 row); each non-zero id is a variant with " +
            "slot 0 or 1. Each variant lists its SpellVisual kit columns (kitSlots: PrecastKit, CastKit, ImpactKit, StateKit, " +
            "StateDoneKit, ChannelKit, CasterImpactKit, TargetImpactKit, MissileTargetingKit, InstantAreaKit, ImpactAreaKit, " +
            "PersistentAreaKit), its SpellVisualMissile rows (by SpellVisualMissileSetID, with SpellVisualEffectName model path " +
            "and SpellMissileMotion) and, when non-zero, unk14MissileMotion: SpellVisual column 14 as a raw value (WoWDBDefs calls it MissileMotion, but almost none of its values are SpellMissileMotion ids; meaning unverified, never resolved). kits is deduplicated across variants and slots; " +
            "reachedVia names every visual/slot/column that reached a kit. Each kit lists its non-zero effect columns " +
            "(Head..World, Special0..2) resolved to SpellVisualEffectName, and its SpellVisualKitModelAttach rows. " +
            "SpellVisualKitAreaModel is not part of the chain (it has no parent-kit column in 5.4.8); SpellMissile is keyed by " +
            "spell and not reachable from it. Kits are capped at 500; truncated is true when the cap withheld one. " +
            "unresolvedReferences lists every non-zero id that has no row, with where it came from. " +
            "store_not_loaded (details.storeUnloaded true) names the missing stores among SpellVisual, SpellVisualKit, " +
            "SpellVisualMissile, SpellVisualEffectName and SpellVisualKitModelAttach.")]
        public async Task<string> GetSpellVisualChain(int id)
        {
            var data = await _data.GetAsync();

            var section = SpellVisualChainResolver.ResolveSection(id, ToolResponse.MaxRows);
            if (!data.Loaded || section.Unavailable)
            {
                // With no client data loaded at all every store is null; naming
                // five files as "did not load" would send an operator after
                // files that may be on disk when the fault is the path.
                var detail = !data.Loaded
                    ? "no client data is loaded at all, so none of them is available."
                    : "these stores did not load: " + string.Join(", ", section.MissingStores) + ".";

                return ToolResponse.Error("store_not_loaded",
                    "No visual chain can be resolved -- it needs SpellVisual, SpellVisualKit, SpellVisualMissile, " +
                    "SpellVisualEffectName and SpellVisualKitModelAttach, and " + detail +
                    " Call server_info to see which stores are missing and why.",
                    new Dictionary<string, object>
                    {
                        ["storeUnloaded"] = true,
                        ["missingStores"] = section.MissingStores.ToList(),
                    });
            }

            if (!SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(id))
                return SpellTools.NotFound(id);

            var result = new Dictionary<string, object> { ["id"] = id };
            foreach (var kv in SpellVisualChainJson.Describe(section))
                result[kv.Key] = kv.Value;
            result["maxKits"] = ToolResponse.MaxRows;

            return ToolResponse.Ok(result);
        }
    }
}
