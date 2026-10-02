// 5.4.8 layout: WoWDBDefs SpellChainEffects.dbd BUILD 5.3.0.16965-5.4.8.18414 (58 header fields / 220 bytes: the five <8> colour/blend columns are packed bytes followed by 3 padding bytes)
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
        [Cardinality(3)] public byte[] Padding = new byte[3];
        public int RenderLayer;
        public float TextureLength;
        public float WavePhase;
        [Cardinality(11)] public int[] SpellChainEffectID = new int[11];
        public string Texture;
    }
}
