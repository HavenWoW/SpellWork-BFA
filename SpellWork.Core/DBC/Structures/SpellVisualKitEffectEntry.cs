using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualKitEffectEntry
    {
        // LAYOUT 90747013, D906832B (WoWDBDefs SpellVisualKitEffect.dbd)
        // marks ID "$noninline,id$" -- Index(true). FieldsCount=2 covers
        // only EffectType and Effect; ID and the relation column below are
        // both excluded, exactly as the brief predicted for a narrow join
        // table with a relation column.
        [Index(true)]
        public int ID;
        // Discriminator selecting how Effect is interpreted (WoWDBDefs'
        // COLUMNS comment: 1 SpellProceduralEffectID, 2
        // SpellVisualKitModelAttachID, 3/4 camera-related, 5 SoundKitID, 6
        // SpellVisualAnimID, 7 ShadowyEffectID, 8 SpellEffectEmissionID, 9
        // OutlineEffectID, 11 DissolveEffectID, 12 EdgeFlowEffectID, 13
        // BeamEffectID, 14 ClientSceneEffectID, 15 CloneEffectID, 16
        // GradientEffectID, 17 BarrageEffectID, 18 RopeEffectID, 19
        // SpellVisualScreenEffectID). Left as a plain integer per the brief
        // -- the resolver interprets it, this structure does not.
        public int EffectType;
        public int Effect;
        // "$noninline,relation$ParentSpellVisualKitID<32>" -- the parent
        // SpellVisualKit id. Plain field, no attribute, declared last: same
        // pattern as the relation columns in SpellVisualEventEntry and
        // SpellVisualMissileEntry. This is also WoWDBDefs' own name for the
        // field, so no rename from the brief's draft was needed.
        public int ParentSpellVisualKitID;
    }
}
