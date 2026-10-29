using System.Collections.Generic;
using System.Linq;

namespace SpellWork.Spell
{
    /// <summary>
    /// The one JSON projection of a <see cref="SpellVisualSection"/>, shared by
    /// get_spell's <c>visual</c> section and get_spell_visual_chain so the two
    /// cannot drift.
    /// </summary>
    public static class SpellVisualChainJson
    {
        /// <summary>
        /// <c>{"storeUnloaded": true, "missingStores": [...]}</c> when a gated
        /// store did not load -- never an empty variants list, which would read
        /// as "this spell has no visual" -- otherwise the chain with
        /// <c>storeUnloaded: false</c>.
        /// </summary>
        public static Dictionary<string, object> Describe(SpellVisualSection section)
        {
            if (section.Unavailable)
            {
                return new Dictionary<string, object>
                {
                    ["storeUnloaded"] = true,
                    ["missingStores"] = section.MissingStores.ToList(),
                };
            }

            var chain = section.Chain;
            return new Dictionary<string, object>
            {
                ["variants"] = chain.Variants.Select(Describe).ToList(),
                ["kits"] = chain.Kits.Select(Describe).ToList(),
                ["unresolvedReferences"] = chain.UnresolvedReferences.ToList(),
                ["truncated"] = chain.Truncated,
                ["storeUnloaded"] = false,
            };
        }

        public static Dictionary<string, object> Describe(SpellVisualVariant variant)
        {
            return new Dictionary<string, object>
            {
                ["slot"] = variant.Slot,
                ["spellVisualId"] = variant.SpellVisualID,
                ["visualResolved"] = variant.VisualResolved,
                ["kitSlots"] = variant.KitSlots.Select(k => new Dictionary<string, object>
                {
                    ["slot"] = k.Slot,
                    ["kitId"] = k.KitID,
                }).ToList(),
                ["missiles"] = variant.Missiles.Select(m => new Dictionary<string, object>
                {
                    ["effectNameId"] = m.EffectNameID,
                    ["name"] = m.Name,
                    ["fileName"] = m.FileName,
                    ["motionId"] = m.MotionID,
                    ["motionName"] = m.MotionName,
                }).ToList(),
            };
        }

        public static Dictionary<string, object> Describe(SpellVisualKitRef kit) => new Dictionary<string, object>
        {
            ["kitId"] = kit.KitID,
            ["reachedVia"] = kit.ReachedVia.ToList(),
            ["effects"] = kit.Effects.Select(e => new Dictionary<string, object>
            {
                ["slot"] = e.Slot,
                ["effectNameId"] = e.EffectNameID,
                ["name"] = e.Name,
                ["fileName"] = e.FileName,
            }).ToList(),
            ["modelAttachments"] = kit.ModelAttachments.Select(a => new Dictionary<string, object>
            {
                ["id"] = a.ID,
                ["attachmentId"] = a.AttachmentID,
                ["effectNameId"] = a.EffectNameID,
                ["name"] = a.Name,
                ["fileName"] = a.FileName,
            }).ToList(),
        };
    }
}
