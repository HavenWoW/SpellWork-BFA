using SpellWork.DBC.Structures;
using SpellWork.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpellWork.Spell
{
    public enum ProcTreeNodeKind
    {
        FamilyMask,
        Skill,
        Spell
    }

    public class ProcTreeNode
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public string ToolTip { get; set; }
        public ProcTreeNodeKind Kind { get; set; }
        public List<ProcTreeNode> Children { get; } = new List<ProcTreeNode>();
    }

    public static class ProcInfo
    {
        public static SpellInfo SpellProc { get; set; }
        public static bool Update = true;

        public static List<ProcTreeNode> BuildFamilyTree(SpellFamilyNames spellfamily)
        {
            var nodes = new List<ProcTreeNode>();

            var spells = from spell in DBC.DBC.SpellInfoStore.Values
                         where spell.SpellFamilyName == (uint)spellfamily

                         join sk in DBC.DBC.SkillLineAbility.Values on spell.ID equals sk.Spell into temp1
                         from skill in temp1.DefaultIfEmpty(new SkillLineAbilityEntry())

                         join skl in DBC.DBC.SkillLine on skill.SkillLine equals skl.Key into temp2
                         from skillLine in temp2.DefaultIfEmpty()

                         select new
                         {
                             spell,
                             skill.SkillLine,
                             skillLine = skillLine.Value
                         };

            for (var i = 0; i < 128; ++i)
            {
                var mask = new uint[4];

                if (i < 32)
                    mask[0] = 1U << i;
                else if (i < 64)
                    mask[1] = 1U << (i - 32);
                else if (i < 96)
                    mask[2] = 1U << (i - 64);
                else
                    mask[3] = 1U << (i - 96);

                var node = new ProcTreeNode
                {
                    Text = $"0x{mask[3]:X8} {mask[2]:X8} {mask[1]:X8} {mask[0]:X8}",
                    Kind = ProcTreeNodeKind.FamilyMask
                };
                nodes.Add(node);
            }

            foreach (var elem in spells)
            {
                var spell = elem.spell;
                var isSkill = elem.SkillLine != 0;

                var name = new StringBuilder();
                var toolTip = new StringBuilder();

                name.AppendFormat("{0} - {1} ", spell.ID, spell.Name);

                toolTip.AppendFormatLine("Spell Name: {0}",  spell.Name);
                toolTip.AppendFormatLine("Description: {0}", spell.Description);
                toolTip.AppendFormatLine("ToolTip: {0}",     spell.Tooltip);

                if (isSkill)
                {
                    name.AppendFormat("(Skill: ({0}) {1}) ", elem.SkillLine, elem.skillLine.DisplayName);

                    toolTip.AppendLine();
                    toolTip.AppendFormatLine("Skill Name: {0}", elem.skillLine.DisplayName);
                    toolTip.AppendFormatLine("Description: {0}", elem.skillLine.Description);
                }

                name.AppendFormat("({0})", ((SpellSchoolMask)spell.SchoolMask).ToString().NormalizeString("MASK_"));

                for (var index = 0; index < nodes.Count; ++index)
                {
                    var mask = new uint[4];

                    if (index < 32)
                        mask[0] = 1U << index;
                    else if (index < 64)
                        mask[1] = 1U << (index - 32);
                    else if (index < 96)
                        mask[2] = 1U << (index - 64);
                    else
                        mask[3] = 1U << (index - 96);

                    if (!spell.SpellClassMask.ContainsElement(mask))
                        continue;

                    var child = new ProcTreeNode
                    {
                        Text = name.ToString(),
                        Key = spell.ID.ToString(),
                        Kind = isSkill ? ProcTreeNodeKind.Skill : ProcTreeNodeKind.Spell,
                        ToolTip = toolTip.ToString()
                    };
                    nodes[index].Children.Add(child);
                }
            }

            return nodes;
        }
    }
}
