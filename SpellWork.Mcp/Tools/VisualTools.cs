using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.Spell;

namespace SpellWork.Mcp.Tools
{
    /// <summary>
    /// get_spell_visual_chain: the CoA 3.3.5a chain (spec §5). Answers from the same
    /// <see cref="SpellVisualChainResolver.ResolveSection(int,int)"/> call that
    /// get_spell's JSON "visual" section and text block use.
    /// </summary>
    [McpServerToolType]
    public sealed class VisualTools
    {
        private readonly SpellDataService _data;

        public VisualTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "get_spell_visual_chain")]
        [Description("The resolved CoA 3.3.5a visual chain for one spell. Takes only id: visuals are not per difficulty. " +
            "Variants come from the spell's own Spell.SpellVisualID[0] and [1]; each non-zero id is a variant with slot 0 or 1. " +
            "Each variant lists its SpellVisual kit columns (kitSlots: PrecastKit, CastKit, ImpactKit, StateKit, StateDoneKit, " +
            "ChannelKit, CasterImpactKit, TargetImpactKit, MissileTargetingKit, InstantAreaKit, ImpactAreaKit, PersistentAreaKit) " +
            "and its missile (missiles: at most one entry, present when SpellVisual.MissileModel or MissileMotion is non-zero): " +
            "effectNameId is MissileModel resolved to SpellVisualEffectName (name, fileName = model path), motionId is MissileMotion " +
            "resolved to SpellMissileMotion (motionName). A negative MissileModel (-1..-5) is not an effect-name id: it is reported " +
            "raw with a null name and never listed as unresolved. kits is deduplicated across variants and slots; reachedVia names " +
            "every visual/slot/column that reached a kit. Each kit lists its non-zero effect columns (Head..World, Special0..2) " +
            "resolved to SpellVisualEffectName, and its SpellVisualKitModelAttach rows (by ParentSpellVisualKitID). " +
            "SpellVisualKitAreaModel is not part of the chain (nothing references it from a kit); Spell.SpellMissileID is not " +
            "followed (only 73% of its non-zero values resolve in SpellMissile on the reference data). Kits are capped at 500; " +
            "truncated is true when the cap withheld one. unresolvedReferences lists every non-zero id that has no row, with where " +
            "it came from. store_not_loaded (details.storeUnloaded true) names the missing stores among SpellVisual, " +
            "SpellVisualKit, SpellVisualEffectName and SpellVisualKitModelAttach.")]
        public async Task<string> GetSpellVisualChain(int id)
        {
            var data = await _data.GetAsync();

            var section = SpellVisualChainResolver.ResolveSection(id, ToolResponse.MaxRows);
            if (!data.Loaded || section.Unavailable)
            {
                // With no client data loaded at all every store is null; naming
                // four files as "did not load" would send an operator after
                // files that may be on disk when the fault is the path.
                var detail = !data.Loaded
                    ? "no client data is loaded at all, so none of them is available."
                    : "these stores did not load: " + string.Join(", ", section.MissingStores) + ".";

                return ToolResponse.Error("store_not_loaded",
                    "No visual chain can be resolved -- it needs SpellVisual, SpellVisualKit, " +
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
