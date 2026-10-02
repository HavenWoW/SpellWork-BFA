using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class AttributeResolutionTests
    {
        [Theory]
        [InlineData("0xFFFFFFFF", true)]
        [InlineData("4294967295", true)]
        [InlineData("0x100000000", false)]
        [InlineData("4294967296", false)]
        [InlineData("13:0x1FFFFFFFF", false)]
        public void AttributeValuesAboveUint32_AreRejected(string text, bool ok)
        {
            var result = SpellTools.TryResolveAttribute(text, out _, out var mask, out var error);
            Assert.Equal(ok, result);
            if (ok) Assert.NotNull(mask); else Assert.NotNull(error);
        }
    }
}
