using System.Drawing;
using System.Windows.Forms;
using SpellWork.Spell;

namespace SpellWork.Extensions
{
    public sealed class RichTextBoxSpellInfoWriter : SpellInfoWriterBase
    {
        private readonly RichTextBox _target;

        public RichTextBoxSpellInfoWriter(RichTextBox target) => _target = target;

        public override void Clear() => _target.Clear();

        public override void AppendText(string text) => _target.AppendText(text);

        public override void SetColor(TextColor color) => _target.SelectionColor = Resolve(color);

        public override void SetStyle(TextColor color, TextStyle style)
            => _target.SetStyle(Resolve(color), Resolve(style));

        public override void SetBold() => _target.SetBold();

        public override void SetDefaultStyle() => _target.SetDefaultStyle();

        private static Color Resolve(TextColor color)
        {
            switch (color)
            {
                case TextColor.Blue:         return Color.Blue;
                case TextColor.DarkBlue:     return Color.DarkBlue;
                case TextColor.DarkRed:      return Color.DarkRed;
                case TextColor.MediumPurple: return Color.MediumPurple;
                case TextColor.Red:          return Color.Red;
                default:                     return Color.Black;
            }
        }

        private static FontStyle Resolve(TextStyle style)
            => style == TextStyle.Bold ? FontStyle.Bold : FontStyle.Regular;
    }
}
