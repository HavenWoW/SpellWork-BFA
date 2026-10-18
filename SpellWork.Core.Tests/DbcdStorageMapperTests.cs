using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class DbcdStorageMapperTests
    {
        private sealed class Sample
        {
            public string Name;
            [Column("EffectPos_facing")] public float Facing;
        }

        [Fact] public void Coerce_widens_boxed_short_to_int() => Assert.Equal(5, DbcdStorageMapper.Coerce((short)5, typeof(int)));
        [Fact] public void Coerce_byte_to_uint() => Assert.Equal(3u, DbcdStorageMapper.Coerce((byte)3, typeof(uint)));
        [Fact] public void Coerce_sbyte_to_int_negative() => Assert.Equal(-1, DbcdStorageMapper.Coerce((sbyte)-1, typeof(int)));
        [Fact] public void Coerce_negative_int_reinterprets_to_uint() => Assert.Equal(uint.MaxValue, DbcdStorageMapper.Coerce((int)-1, typeof(uint)));
        [Fact] public void Coerce_int_to_enum() => Assert.Equal(System.DayOfWeek.Tuesday, DbcdStorageMapper.Coerce((int)2, typeof(System.DayOfWeek)));
        [Fact] public void Coerce_null_array_target_returns_empty() => Assert.Empty((uint[])DbcdStorageMapper.Coerce(null, typeof(uint[])));
        [Fact] public void Coerce_scalar_to_array_throws() => Assert.Throws<System.InvalidOperationException>(() => DbcdStorageMapper.Coerce(5, typeof(uint[])));
        [Fact] public void Coerce_int_array_elementwise_to_uint() => Assert.Equal(new uint[] { 1, 2 }, (uint[])DbcdStorageMapper.Coerce(new int[] { 1, 2 }, typeof(uint[])));

        [Fact]
        public void ResolveColumn_prefers_exact_then_lang()
        {
            var f = typeof(Sample).GetField("Name");
            Assert.Equal("Name_lang", DbcdStorageMapper.ResolveColumn(f, new[] { "ID", "Name_lang" }));
            Assert.Equal("Name", DbcdStorageMapper.ResolveColumn(f, new[] { "ID", "Name", "Name_lang" }));
        }

        [Fact]
        public void ResolveColumn_honours_Column_attribute()
        {
            var f = typeof(Sample).GetField("Facing");
            Assert.Equal("EffectPos_facing", DbcdStorageMapper.ResolveColumn(f, new[] { "ID" }));
        }
    }
}
