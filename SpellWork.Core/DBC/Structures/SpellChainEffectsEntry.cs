// CoA 3.3.5a layout: WoWDBDefs SpellChainEffects.dbd 3.3.5.12340 (48 fields, 177 bytes; not loaded by the CoA server). Verified on the reference data: 39 four-byte columns, the five uint8 colour/blend columns packed with no padding (bytes 156-160), then four more four-byte columns; Combo is a string-table offset in every row
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellChainEffectsEntry
    {
        [Index(true)] public int ID;
        public float AvgSegLen;
        public float Width;
        public float NoiseScale;
        public float TexCoordScale;
        public int SegDuration;
        public int SegDelay;
        public string Texture;
        public int Flags;
        public int JointCount;
        public float JointOffsetRadius;
        public int JointsPerMinorJoint;
        public int MinorJointsPerMajorJoint;
        public float MinorJointScale;
        public float MajorJointScale;
        public float JointMoveSpeed;
        public float JointSmoothness;
        public float MinDurationBetweenJointJumps;
        public float MaxDurationBetweenJointJumps;
        public float WaveHeight;
        public float WaveFreq;
        public float WaveSpeed;
        public float MinWaveAngle;
        public float MaxWaveAngle;
        public float MinWaveSpin;
        public float MaxWaveSpin;
        public float ArcHeight;
        public float MinArcAngle;
        public float MaxArcAngle;
        public float MinArcSpin;
        public float MaxArcSpin;
        public float DelayBetweenEffects;
        public float MinFlickerOnDuration;
        public float MaxFlickerOnDuration;
        public float MinFlickerOffDuration;
        public float MaxFlickerOffDuration;
        public float PulseSpeed;
        public float PulseOnLength;
        public float PulseFadeLength;
        public byte Alpha;
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte BlendMode;
        public string Combo;
        public int RenderLayer;
        public float TextureLength;
        public float WavePhase;
    }
}
