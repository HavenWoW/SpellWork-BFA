using System;

namespace SpellWork.Spell
{
    public abstract class SpellInfoWriterBase : ISpellInfoWriter
    {
        public abstract void Clear();
        public abstract void AppendText(string text);

        public abstract void SetColor(TextColor color);
        public abstract void SetStyle(TextColor color, TextStyle style);
        public abstract void SetBold();
        public abstract void SetDefaultStyle();

        public void Append(object text) => AppendFormat("{0}", text);

        public void AppendLine() => AppendText(Environment.NewLine);

        public void AppendLine(string text) => AppendText(text + Environment.NewLine);

        public void AppendFormat(string format, params object[] args)
            => AppendText(string.Format(FrameworkNumberFormat.Instance, format, args));

        public void AppendFormatLine(string format, params object[] args)
            => AppendText(string.Format(FrameworkNumberFormat.Instance, format, args) + Environment.NewLine);

        public void AppendFormatIfNotNull<T>(string format, T arg)
        {
            if (!IsValueConsideredNullForFormatting(arg))
                AppendFormat(format, arg);
        }

        public void AppendFormatLineIfNotNull<T>(string format, T arg)
        {
            if (!IsValueConsideredNullForFormatting(arg))
                AppendFormatLine(format, arg);
        }

        private static bool IsValueConsideredNullForFormatting<T>(T arg)
        {
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                    return true;
                case TypeCode.Object:
                    return arg == null;
                case TypeCode.Boolean:
                    return false; // always print a boolean
                case TypeCode.Char:
                    return Convert.ToChar(arg) == '\0';
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                    return Convert.ToInt64(arg) == 0L;
                case TypeCode.UInt64:
                    return Convert.ToUInt64(arg) == 0uL;
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return Math.Abs(Convert.ToDecimal(arg)) <= 1.0E-5M;
                case TypeCode.DateTime:
                    return false;
                case TypeCode.String:
                    return string.IsNullOrEmpty(Convert.ToString(arg));
                default:
                    break;
            }

            return true;
        }
    }
}
