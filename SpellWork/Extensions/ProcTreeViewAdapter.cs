using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SpellWork.Spell;

namespace SpellWork.Extensions
{
    public static class ProcTreeViewAdapter
    {
        public static void Populate(TreeView target, List<ProcTreeNode> nodes)
        {
            target.Nodes.Clear();
            foreach (var node in nodes)
                target.Nodes.Add(Convert(node));
        }

        private static TreeNode Convert(ProcTreeNode source)
        {
            var node = new TreeNode(source.Text)
            {
                Name = source.Key ?? string.Empty,
                ToolTipText = source.ToolTip ?? string.Empty
            };

            // The three kinds carry the presentation the headless model
            // deliberately does not: a mask node keeps the TreeView's default
            // fore colour, which is why this is a switch and not a ternary on
            // a boolean.
            switch (source.Kind)
            {
                case ProcTreeNodeKind.Skill:
                    node.ImageKey = "plus.ico";
                    node.ForeColor = Color.Blue;
                    break;
                case ProcTreeNodeKind.Spell:
                    node.ImageKey = "munus.ico";
                    node.ForeColor = Color.Red;
                    break;
                default:
                    node.ImageKey = @"family.ico";
                    break;
            }

            foreach (var child in source.Children)
                node.Nodes.Add(Convert(child));

            return node;
        }
    }
}
