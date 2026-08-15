using System.Text;

namespace SpellWork.Spell
{
    public sealed class PlainTextSpellInfoWriter : SpellInfoWriterBase
    {
        private readonly StringBuilder _builder = new StringBuilder();

        public override void Clear() => _builder.Clear();

        public override void AppendText(string text) => _builder.Append(text);

        public override void SetColor(TextColor color) { }

        public override void SetStyle(TextColor color, TextStyle style) { }

        public override void SetBold() { }

        public override void SetDefaultStyle() { }

        public override string ToString() => _builder.ToString();
    }
}
