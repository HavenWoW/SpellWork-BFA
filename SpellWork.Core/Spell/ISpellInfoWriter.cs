namespace SpellWork.Spell
{
    // Only the six colours and two styles SpellInfo actually uses, so Core
    // needs no System.Drawing reference.
    public enum TextColor { Black, Blue, DarkBlue, DarkRed, MediumPurple, Red }

    public enum TextStyle { Regular, Bold }

    public interface ISpellInfoWriter
    {
        void Clear();
        void Append(object text);
        void AppendText(string text);
        void AppendLine();
        void AppendLine(string text);
        void AppendFormat(string format, params object[] args);
        void AppendFormatLine(string format, params object[] args);
        void AppendFormatIfNotNull<T>(string format, T arg);
        void AppendFormatLineIfNotNull<T>(string format, T arg);
        void SetColor(TextColor color);
        void SetStyle(TextColor color, TextStyle style);
        void SetBold();
        void SetDefaultStyle();
    }
}
