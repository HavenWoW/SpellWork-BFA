using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class LocaleSlotTests
    {
        [Theory]
        [InlineData("enUS", 0)]
        [InlineData("koKR", 1)]
        [InlineData("frFR", 2)]
        [InlineData("deDE", 3)]
        [InlineData("zhCN", 4)]
        [InlineData("zhTW", 5)]
        [InlineData("esES", 6)]
        [InlineData("esMX", 7)]
        [InlineData("ruRU", 8)]
        public void Maps_names(string locale, int slot)
        {
            Assert.Equal(slot, LocaleSlots.Resolve(locale, out var fallback));
            Assert.False(fallback);
        }

        [Theory]
        [InlineData("xxXX")]
        [InlineData("")]
        [InlineData(null)]
        public void Unknown_is_slot_0_with_fallback(string locale)
        {
            Assert.Equal(0, LocaleSlots.Resolve(locale, out var fallback));
            Assert.True(fallback);
        }
    }
}
