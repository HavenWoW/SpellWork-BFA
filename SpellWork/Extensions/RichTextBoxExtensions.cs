using System;
using System.Drawing;
using System.Windows.Forms;

namespace SpellWork.Extensions
{
    public static class RichTextBoxExtensions
    {
        public const string DefaultFamily = "Arial Unicode MS";
        public const float DefaultSize = 9f;

        public static void AppendFormatLine(this RichTextBox textbox, string format, params object[] arg0)
        {
            textbox.AppendText(string.Format(format, arg0) + Environment.NewLine);
        }

        public static void AppendFormat(this RichTextBox textbox, string format, params object[] arg0)
        {
            textbox.AppendText(string.Format(format, arg0));
        }

        public static void AppendLine(this RichTextBox textbox)
        {
            textbox.AppendText(Environment.NewLine);
        }

        public static void AppendLine(this RichTextBox textbox, string text)
        {
            textbox.AppendText(text + Environment.NewLine);
        }

        public static void Append(this RichTextBox textbox, object text)
        {
            textbox.AppendText(text.ToString());
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

        public static void AppendFormatLineIfNotNull<T>(this RichTextBox builder, string format, T arg)
        {
            if (!IsValueConsideredNullForFormatting(arg))
                builder.AppendFormatLine(format, arg);
        }

        public static void AppendFormatIfNotNull<T>(this RichTextBox builder, string format, T arg)
        {
            if (!IsValueConsideredNullForFormatting(arg))
                builder.AppendFormat(format, arg);
        }

        public static void SetStyle(this RichTextBox textbox, Color color, FontStyle style)
        {
            textbox.SelectionColor = color;
            textbox.SelectionFont = new Font(DefaultFamily, DefaultSize, style);
        }

        public static void SetBold(this RichTextBox textbox)
        {
            textbox.SelectionFont = new Font(DefaultFamily, DefaultSize, FontStyle.Bold);
        }

        public static void SetDefaultStyle(this RichTextBox textbox)
        {
            textbox.SelectionFont = new Font(DefaultFamily, DefaultSize, FontStyle.Regular);
            textbox.SelectionColor = Color.Black;
        }

        public static void ColorizeCode(this RichTextBox rtb)
        {
            string[] keywords = { "INSERT", "INTO", "DELETE", "FROM", "IN", "VALUES", "WHERE" };
            var text = rtb.Text;

            rtb.SelectAll();
            rtb.SelectionColor = rtb.ForeColor;

            foreach (var keyword in keywords)
            {
                var keywordPos = rtb.Find(keyword, RichTextBoxFinds.MatchCase | RichTextBoxFinds.WholeWord);

                while (keywordPos != -1)
                {
                    var commentPos = text.LastIndexOf("-- ", keywordPos, StringComparison.OrdinalIgnoreCase);
                    var newLinePos = text.LastIndexOf("\n", keywordPos, StringComparison.OrdinalIgnoreCase);

                    var quoteCount = 0;
                    var quotePos = text.IndexOf("\"", newLinePos + 1, keywordPos - newLinePos, StringComparison.OrdinalIgnoreCase);

                    for (; quotePos != -1; quoteCount++)
                        quotePos = text.IndexOf("\"", quotePos + 1, keywordPos - (quotePos + 1), StringComparison.OrdinalIgnoreCase);

                    if (newLinePos >= commentPos && quoteCount % 2 == 0)
                        rtb.SelectionColor = Color.Blue;
                    else if (newLinePos == commentPos)
                        rtb.SelectionColor = Color.Green;

                    keywordPos = rtb.Find(keyword, keywordPos + rtb.SelectionLength, RichTextBoxFinds.MatchCase | RichTextBoxFinds.WholeWord);
                }
            }

            rtb.Select(0, 0);
        }
    }
}
