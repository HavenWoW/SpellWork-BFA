using System;
using System.Globalization;

namespace SpellWork.Spell
{
    // SpellWork.Core is compiled for netstandard2.0 and runs in two hosts: the
    // net48 GUI and a net9 MCP server. Those runtimes format floats differently,
    // in two separate ways, and the goldens demand byte-identical output:
    //
    //   "F"  -- .NET Framework formats from a 7-significant-digit view of the
    //           float and rounds midpoints away from zero; .NET Core 3.0+
    //           formats the exact value and rounds half-to-even. 1.125f prints
    //           as "1.13" on net48 and "1.12" on net9.
    //   "G"  -- .NET Framework's default float ToString is 7 significant digits
    //           (15 for double); .NET Core 3.0+ emits the shortest round-trippable
    //           string. 1f/3f prints as "0.3333333" on net48 and "0.33333334"
    //           on net9.
    //
    // Routing every writer format call through this provider fixes both, and
    // pins the culture at the same time -- string.Format's culture-sensitive
    // overload would otherwise emit "0,88" on a de-DE machine.
    internal sealed class FrameworkNumberFormat : IFormatProvider, ICustomFormatter
    {
        public static readonly FrameworkNumberFormat Instance = new FrameworkNumberFormat();

        // decimal's max magnitude is ~7.9e28; float/double can exceed that
        // easily, and the (decimal) cast throws OverflowException rather than
        // saturating, so it has to be guarded before it ever runs.
        private const double MaxDecimalConvertibleMagnitude = 7.9e28;

        private FrameworkNumberFormat() { }

        public object GetFormat(Type formatType)
            => formatType == typeof(ICustomFormatter)
                ? this
                : CultureInfo.InvariantCulture.GetFormat(formatType);

        // Returning null hands the argument back to string.Format, which falls
        // back to IFormattable.ToString(format, this) -- and GetFormat above
        // routes that to InvariantCulture. So non-float arguments need no
        // special handling here and still format invariantly.
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg is float f)
                return FormatSingle(format, f);

            if (arg is double d)
                return FormatDouble(format, d);

            return null;
        }

        // The (decimal) cast here is what reproduces the Framework view: the
        // float-to-decimal conversion rounds to 7 significant digits. Widening
        // to double first (as the shared "value" field would) and casting that
        // to decimal instead rounds to 15 significant digits, which is exactly
        // the *wrong* answer -- so float and double each need their own path
        // through their own (decimal) cast, not a shared helper over a widened
        // double.
        private static string FormatSingle(string format, float value)
        {
            if (TryGetFixedPointDigits(format, out var digits))
            {
                if (float.IsNaN(value) || float.IsInfinity(value) ||
                    Math.Abs(value) >= MaxDecimalConvertibleMagnitude ||
                    digits < 0 || digits > 15)
                    return value.ToString(format, CultureInfo.InvariantCulture);

                return Math.Round((decimal)value, digits, MidpointRounding.AwayFromZero)
                    .ToString("F" + digits, CultureInfo.InvariantCulture);
            }

            if (IsDefaultOrGeneral(format))
                return value.ToString("G7", CultureInfo.InvariantCulture);

            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string FormatDouble(string format, double value)
        {
            if (TryGetFixedPointDigits(format, out var digits))
            {
                if (double.IsNaN(value) || double.IsInfinity(value) ||
                    Math.Abs(value) >= MaxDecimalConvertibleMagnitude ||
                    digits < 0 || digits > 15)
                    return value.ToString(format, CultureInfo.InvariantCulture);

                return Math.Round((decimal)value, digits, MidpointRounding.AwayFromZero)
                    .ToString("F" + digits, CultureInfo.InvariantCulture);
            }

            if (IsDefaultOrGeneral(format))
                return value.ToString("G15", CultureInfo.InvariantCulture);

            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static bool IsDefaultOrGeneral(string format)
            => string.IsNullOrEmpty(format) || format == "G" || format == "g";

        // "F" / "f" alone mean 2 digits, matching NumberFormatInfo.InvariantInfo.NumberDecimalDigits.
        // "F3" / "f3" mean 3 digits, and so on. Anything else (no leading F/f,
        // or digits that don't parse -- e.g. "F+1") is not fixed-point.
        private static bool TryGetFixedPointDigits(string format, out int digits)
        {
            digits = 0;

            if (string.IsNullOrEmpty(format) || (format[0] != 'F' && format[0] != 'f'))
                return false;

            if (format.Length == 1)
            {
                digits = NumberFormatInfo.InvariantInfo.NumberDecimalDigits;
                return true;
            }

            return int.TryParse(format.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out digits);
        }
    }
}
