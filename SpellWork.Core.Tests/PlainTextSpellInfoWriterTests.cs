using System.Globalization;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class PlainTextSpellInfoWriterTests
    {
        [Fact]
        public void AppendFormatLine_appends_formatted_text_and_a_newline()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLine("ID - {0} {1}", 133, "Fireball");
            Assert.Equal("ID - 133 Fireball\n", writer.ToString().Replace("\r\n", "\n"));
        }

        [Fact]
        public void Styling_calls_do_not_emit_text()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.SetBold();
            writer.SetStyle(TextColor.Blue, TextStyle.Bold);
            writer.SetColor(TextColor.Red);
            writer.SetDefaultStyle();
            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public void Clear_discards_everything_written_so_far()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendText("discarded");
            writer.Clear();
            Assert.Equal("", writer.ToString());
        }

        // Mirrors RichTextBoxExtensions.IsValueConsideredNullForFormatting so the
        // two writers cannot disagree about what counts as "null" for formatting.
        [Theory]
        [InlineData(0, false)]
        [InlineData(5, true)]
        public void AppendFormatLineIfNotNull_skips_zero_ints(int value, bool expectOutput)
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLineIfNotNull("v: {0}", value);
            Assert.Equal(expectOutput, writer.ToString().Length > 0);
        }

        [Fact]
        public void AppendFormatLineIfNotNull_skips_empty_strings()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLineIfNotNull("v: {0}", "");
            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public void AppendFormatLineIfNotNull_always_prints_booleans()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLineIfNotNull("v: {0}", false);
            Assert.Equal("v: False\n", writer.ToString().Replace("\r\n", "\n"));
        }

        // .NET Framework formats "F" from a 7-significant-digit view of a float
        // and rounds midpoints away from zero; .NET Core 3.0+ formats the exact
        // value and rounds half-to-even. FrameworkNumberFormat reproduces the
        // Framework view on both runtimes, via the writer (not by calling the
        // provider directly), because the point is that AppendFormat wires it up.
        [Theory]
        [InlineData(1.125f, "1.13")]
        [InlineData(-1.125f, "-1.13")]
        [InlineData(1f, "1.00")]
        [InlineData(0.004f, "0.00")]
        public void AppendFormat_reproduces_framework_fixed_point_rounding(float value, string expected)
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormat("{0:F}", value);
            Assert.Equal(expected, writer.ToString());
        }

        [Fact]
        public void AppendFormat_is_independent_of_the_current_culture()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                var writer = new PlainTextSpellInfoWriter();
                writer.AppendFormat("{0:F}", 1.5f);

                Assert.Equal("1.50", writer.ToString());
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        // Pins the behaviour defect B's carve-out protects: suppression must
        // still read the raw, unrounded value. Rounding 0.004 to "0.00" first
        // and then testing the rounded value against the suppression threshold
        // would wrongly suppress a Speed of 0.004; testing the raw value first
        // (as IsValueConsideredNullForFormatting does) keeps it visible.
        [Fact]
        public void AppendFormatLineIfNotNull_still_prints_a_speed_that_rounds_to_zero()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLineIfNotNull("Speed {0:F}", 0.004f);
            Assert.Equal("Speed 0.00\n", writer.ToString().Replace("\r\n", "\n"));
        }

        [Fact]
        public void AppendFormatLineIfNotNull_suppresses_a_speed_below_the_threshold()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLineIfNotNull("Speed {0:F}", 0.000004f);
            Assert.Equal("", writer.ToString());
        }
    }
}
