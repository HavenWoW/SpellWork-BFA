using SpellWork.DBC;
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

    /// <summary>
    /// 3.3.5 proc data. A spell's proc flags, chance and charges are the
    /// ProcTypeMask/ProcChance/ProcCharges columns of its Spell row (there is
    /// no SpellAuraOptions table); bits decode through the CoA
    /// <see cref="ProcFlags"/> enum (SpellMgr.h).
    /// </summary>
    public static class ProcInfo
    {
        public static SpellInfo SpellProc { get; set; }
        public static bool Update = true;

        /// <summary>
        /// One line per set bit, "  NN NAME" with NN the bit position; a bit
        /// with no <see cref="ProcFlags"/> member reads "  NN (unknown bit)".
        /// </summary>
        public static string DescribeProcFlags(uint procFlags)
        {
            var sb = new StringBuilder();
            for (var bit = 0; bit < 32; ++bit)
            {
                var mask = 1u << bit;
                if ((procFlags & mask) == 0)
                    continue;

                var name = System.Enum.GetName(typeof(ProcFlags), unchecked((int)mask)) ?? "(unknown bit)";
                sb.AppendFormatLine("  {0:D2} {1}", bit, name);
            }

            return sb.ToString();
        }

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
                             SkillLine = skill.SkillLine,
                             skillLine = skillLine.Value
                         };

            // 3.3.5 family masks are 96 bits (three words).
            for (var i = 0; i < 96; ++i)
            {
                var mask = new uint[3];

                if (i < 32)
                    mask[0] = 1U << i;
                else if (i < 64)
                    mask[1] = 1U << (i - 32);
                else
                    mask[2] = 1U << (i - 64);

                var node = new ProcTreeNode
                {
                    Text = $"0x{mask[2]:X8} {mask[1]:X8} {mask[0]:X8}",
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
                    name.AppendFormat("(Skill: ({0}) {1}) ", elem.SkillLine, elem.skillLine?.DisplayName);

                    toolTip.AppendLine();
                    toolTip.AppendFormatLine("Skill Name: {0}", elem.skillLine?.DisplayName);
                    toolTip.AppendFormatLine("Description: {0}", elem.skillLine == null ? null : LocaleSlots.Pick(elem.skillLine.DescriptionLang));
                }

                name.AppendFormat("({0})", ((SpellSchoolMask)spell.SchoolMask).ToString().NormalizeString("MASK_"));

                for (var index = 0; index < nodes.Count; ++index)
                {
                    var mask = new uint[3];

                    if (index < 32)
                        mask[0] = 1U << index;
                    else if (index < 64)
                        mask[1] = 1U << (index - 32);
                    else
                        mask[2] = 1U << (index - 64);

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
