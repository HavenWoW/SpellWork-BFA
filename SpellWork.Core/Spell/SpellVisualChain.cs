using System.Collections.Generic;

namespace SpellWork.Spell
{
    /// <summary>
    /// The 5.4.8 visual chain for one spell (spec §6):
    /// <c>SpellMisc.SpellVisualID[0..1]</c> → <c>SpellVisual</c> → kits,
    /// missiles, effect names and model attachments. Visuals are not per
    /// difficulty (D4); the difficulty-0 SpellMisc row is the source.
    /// </summary>
    public sealed class SpellVisualChain
    {
        public int SpellID { get; set; }
        public IReadOnlyList<SpellVisualVariant> Variants { get; set; }

        /// <summary>Deduplicated across variants and slots.</summary>
        public IReadOnlyList<SpellVisualKitRef> Kits { get; set; }

        /// <summary>True when the kit cap withheld at least one kit that resolves.</summary>
        public bool Truncated { get; set; }

        /// <summary>Every non-zero id the walk could not resolve, with where it came
        /// from, e.g. <c>"SpellVisualKit#123 (from SpellVisual#67.ImpactKit)"</c>.</summary>
        public IReadOnlyList<string> UnresolvedReferences { get; set; }
    }

    public sealed class SpellVisualVariant
    {
        /// <summary>0 or 1: the index into <c>SpellMisc.SpellVisualID</c>.</summary>
        public int Slot { get; set; }
        public int SpellVisualID { get; set; }
        public bool VisualResolved { get; set; }

        /// <summary>Every non-zero kit column of the SpellVisual row, resolved or not.</summary>
        public IReadOnlyList<KitSlotRef> KitSlots { get; set; }
        public IReadOnlyList<SpellVisualMissileInfo> Missiles { get; set; }

        /// <summary>
        /// SpellVisual column 14 (WoWDBDefs "MissileMotion"), raw. Its meaning
        /// is unverified -- almost none of its values are SpellMissileMotion
        /// ids -- so it is reported as-is, never resolved and never listed in
        /// UnresolvedReferences. 0 when the column is 0.
        /// </summary>
        public int Unk14MissileMotion { get; set; }
    }

    public sealed class KitSlotRef
    {
        public string Slot { get; set; }
        public int KitID { get; set; }
    }

    public sealed class SpellVisualKitRef
    {
        public int KitID { get; set; }

        /// <summary>Every path that reached this kit, e.g. <c>"visual 67 (slot 0) PrecastKit"</c>.</summary>
        public IReadOnlyList<string> ReachedVia { get; set; }

        /// <summary>The non-zero Head..World / Special[i] effect columns.</summary>
        public IReadOnlyList<KitEffectRef> Effects { get; set; }
        public IReadOnlyList<KitModelAttachInfo> ModelAttachments { get; set; }
    }

    public sealed class KitEffectRef
    {
        public string Slot { get; set; }
        public int EffectNameID { get; set; }

        /// <summary>Null when the effect name id does not resolve.</summary>
        public string Name { get; set; }
    }

    public sealed class KitModelAttachInfo
    {
        public int ID { get; set; }
        public int AttachmentID { get; set; }
        public int EffectNameID { get; set; }
        public string Name { get; set; }
    }

    public sealed class SpellVisualMissileInfo
    {
        public int ID { get; set; }
        public int EffectNameID { get; set; }
        public string Name { get; set; }
        public int MotionID { get; set; }
        public string MotionName { get; set; }
    }
}
