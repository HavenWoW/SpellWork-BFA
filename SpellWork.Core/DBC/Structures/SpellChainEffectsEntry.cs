using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellChainEffectsEntry
    {
        // LAYOUT 461F9829, 31DC7CA9 (WoWDBDefs SpellChainEffects.dbd) marks
        // ID "$noninline,id$" -- Index(true). Widest table in the visual
        // chain: 60 fields after the ID, none of them arrays wider than 11.
        // Types below come from this LAYOUT block's own width/sign
        // annotations (<u8>, <u16>, <u32>, <32>); COLUMNS only gives the
        // generic "int"/"float" declaration, which is the union across every
        // layout this table has ever had (it also lists a "Width" field that
        // this particular layout does not carry -- schema drift between
        // builds, not a lookup error).
        [Index(true)]
        public int ID;
        public float AvgSegLen;
        public float NoiseScale;
        public float TexCoordScale;
        public uint SegDuration;
        public ushort SegDelay;
        public uint Flags;
        public ushort JointCount;
        public float JointOffsetRadius;
        public byte JointsPerMinorJoint;
        public byte MinorJointsPerMajorJoint;
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
        public byte RenderLayer;
        public float WavePhase;
        public float TimePerFlipFrame;
        public float VariancePerFlipFrame;
        public uint TextureParticleFileDataID;
        public float StartWidth;
        public float EndWidth;
        public ushort WidthScaleCurveID;
        public byte NumFlipFramesU;
        public byte NumFlipFramesV;
        public uint SoundKitID;
        public float ParticleScaleMultiplier;
        public float ParticleEmissionRateMultiplier;
        // Self-referencing: up to 11 sibling SpellChainEffects rows (e.g.
        // alternate segments/LODs of the same chain). Zero means unused.
        [Cardinality(11)]
        public ushort[] SpellChainEffectID = new ushort[11];
        [Cardinality(3)]
        public float[] TextureCoordScaleU = new float[3];
        [Cardinality(3)]
        public float[] TextureCoordScaleV = new float[3];
        [Cardinality(3)]
        public float[] TextureRepeatLengthU = new float[3];
        [Cardinality(3)]
        public float[] TextureRepeatLengthV = new float[3];
        [Cardinality(3)]
        public int[] TextureFileDataID = new int[3];
    }
}
