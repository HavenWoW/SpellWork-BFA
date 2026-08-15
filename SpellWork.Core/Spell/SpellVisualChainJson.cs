using System.Collections.Generic;
using System.Linq;

namespace SpellWork.Spell
{
    /// <summary>
    /// The per-node JSON projection of <see cref="SpellVisualChain"/>'s POCO
    /// graph: one <see cref="Dictionary{TKey,TValue}"/> per variant, kit,
    /// effect and missile, with the key names and key order both hosts emit.
    /// <para>
    /// Deliberately <b>not</b> a serialiser -- it returns dictionaries, never a
    /// string, and knows nothing about the envelope around them. The envelope
    /// is host-specific and stays host-specific: the MCP tool's payload carries
    /// <c>maxKits</c> (from <c>ToolResponse.MaxRows</c>, an Mcp-only constant)
    /// while <see cref="SpellInfoJsonSerializer"/>'s <c>visual</c> section
    /// carries <c>storeUnloaded</c>, which the MCP tool has no use for because
    /// it refuses the call outright in that state. Only the per-node shape is
    /// shared, because only the per-node shape is the same in both.
    /// </para>
    /// <para>
    /// This lives in Core rather than beside the MCP tool because Core cannot
    /// reference <c>SpellWork.Mcp</c> -- the dependency runs the other way --
    /// and duplicating the projection would leave two independent JSON views of
    /// one POCO graph free to drift apart, which is the exact failure putting
    /// <see cref="SpellVisualChainResolver"/> in Core was meant to prevent.
    /// </para>
    /// </summary>
    public static class SpellVisualChainJson
    {
        public static Dictionary<string, object> Describe(SpellVisualVariant variant)
        {
            var dict = new Dictionary<string, object>
            {
                ["spellVisualId"] = variant.SpellVisualID,
                ["difficultyId"] = variant.DifficultyID,
                ["probability"] = variant.Probability,
                ["priority"] = variant.Priority,
                ["visualResolved"] = variant.VisualResolved,
                ["kits"] = variant.Kits.Select(Describe).ToList(),
            };

            if (variant.Missile != null)
                dict["missile"] = Describe(variant.Missile);

            return dict;
        }

        public static Dictionary<string, object> Describe(SpellVisualKitRef kit) => new Dictionary<string, object>
        {
            ["kitId"] = kit.KitID,
            ["reachedVia"] = kit.ReachedVia,
            ["effects"] = kit.Effects.Select(Describe).ToList(),
        };

        // Raw ids, carried through exactly as SpellVisualKitEffectInfo holds
        // them -- see that type's own remarks for why no enum is invented
        // here: only effectType 2 has a confirmed target in this build's data.
        public static Dictionary<string, object> Describe(SpellVisualKitEffectInfo effect) => new Dictionary<string, object>
        {
            ["effectType"] = effect.EffectType,
            ["effect"] = effect.Effect,
        };

        // Key named "spellVisualMissileId", deliberately not "missileId":
        // SpellMissileInfo.MissileID is a SpellVisualMissile row id, not a
        // SpellMissile id, and an agent reading "missileId" would reasonably
        // assume the latter -- see that property's own doc comment and the
        // resolver's class remarks for the measurement establishing this.
        public static Dictionary<string, object> Describe(SpellMissileInfo missile) => new Dictionary<string, object>
        {
            ["spellVisualMissileId"] = missile.MissileID,
            ["motionId"] = missile.MotionID,
            ["motionName"] = missile.MotionName,
        };
    }
}
