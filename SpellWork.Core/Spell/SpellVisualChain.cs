using System.Collections.Generic;

namespace SpellWork.Spell
{
    /// <summary>
    /// The CoA 3.3.5a visual chain for one spell (spec §5):
    /// <c>Spell.SpellVisualID[0..1]</c> → <c>SpellVisual</c> → kits, the
    /// missile, effect names and model attachments. Visuals are not per
    /// difficulty; the requested Spell row is the source.
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
        /// <summary>0 or 1: the index into <c>Spell.SpellVisualID</c>.</summary>
        public int Slot { get; set; }
        public int SpellVisualID { get; set; }
        public bool VisualResolved { get; set; }

        /// <summary>Every non-zero kit column of the SpellVisual row, resolved or not.</summary>
        public IReadOnlyList<KitSlotRef> KitSlots { get; set; }

        /// <summary>The SpellVisual row's own missile: one entry when
        /// MissileModel or MissileMotion is non-zero, else empty.</summary>
        public IReadOnlyList<SpellVisualMissileInfo> Missiles { get; set; }
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

        /// <summary>The model path (SpellVisualEffectName.FileName); null when unresolved.</summary>
        public string FileName { get; set; }
    }

    public sealed class KitModelAttachInfo
    {
        public int ID { get; set; }
        public int AttachmentID { get; set; }
        public int EffectNameID { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }
    }

    /// <summary>
    /// SpellVisual.MissileModel and MissileMotion. A positive MissileModel is a
    /// SpellVisualEffectName id; a negative one (-1..-5 on the reference data)
    /// is not an id and is kept raw with a null Name, never listed as unresolved.
    /// </summary>
    public sealed class SpellVisualMissileInfo
    {
        /// <summary>SpellVisual.MissileModel, raw.</summary>
        public int EffectNameID { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }

        /// <summary>SpellVisual.MissileMotion, a SpellMissileMotion id; 0 for none.</summary>
        public int MotionID { get; set; }
        public string MotionName { get; set; }
    }
}
