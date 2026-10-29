using System.Collections.Generic;
using System.Linq;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class ProcInfoTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public ProcInfoTests(ClientDataFixture fixture) => _fixture = fixture;

        // 3.3.5: proc flags come from Spell.ProcTypeMask and decode through
        // the CoA ProcFlags enum (SpellMgr.h), one line per set bit.
        [Fact]
        public void DescribeProcFlags_names_each_set_bit_from_the_CoA_enum()
        {
            // Omen Clearcasting's mask: bits 4, 8, 10, 12, 14, 16; bit 30 has no member.
            var lines = ProcInfo.DescribeProcFlags(0x00015510u | (1u << 30))
                .Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(new[]
            {
                "  04 PROC_FLAG_DONE_SPELL_MELEE_DMG_CLASS",
                "  08 PROC_FLAG_DONE_SPELL_RANGED_DMG_CLASS",
                "  10 PROC_FLAG_DONE_SPELL_NONE_DMG_CLASS_POS",
                "  12 PROC_FLAG_DONE_SPELL_NONE_DMG_CLASS_NEG",
                "  14 PROC_FLAG_DONE_SPELL_MAGIC_DMG_CLASS_POS",
                "  16 PROC_FLAG_DONE_SPELL_MAGIC_DMG_CLASS_NEG",
                "  30 (unknown bit)",
            }, lines);

            Assert.Equal("", ProcInfo.DescribeProcFlags(0));
            Assert.Contains("24 PROC_FLAG_DEATH", ProcInfo.DescribeProcFlags(0x01000000));
        }

        [SkippableFact]
        public void BuildFamilyTree_returns_one_node_per_mask_bit()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var nodes = ProcInfo.BuildFamilyTree(SpellFamilyNames.SPELLFAMILY_MAGE);

            // 3.3.5 family masks are 96 bits.
            Assert.Equal(96, nodes.Count);
            Assert.All(nodes, n => Assert.Equal(ProcTreeNodeKind.FamilyMask, n.Kind));
            Assert.Equal("0x00000000 00000000 00000001", nodes[0].Text);
            Assert.Equal("0x80000000 00000000 00000000", nodes[95].Text);
        }

        [SkippableFact]
        public void BuildFamilyTree_attaches_the_familys_spells_to_their_mask_bits()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var nodes = ProcInfo.BuildFamilyTree(SpellFamilyNames.SPELLFAMILY_MAGE);
            var spells = nodes.SelectMany(n => n.Children).ToList();

            // The 96 mask nodes exist whether or not the join found anything,
            // so asserting on `nodes` alone would pass against an empty family.
            // These assertions fail if the traversal stops finding spells.
            Assert.NotEmpty(spells);

            // Kind is checked against SkillLineAbility data below, not against
            // itself: `s.Kind == Skill || s.Kind == Spell` is true of every
            // child that could possibly exist and catches nothing.
            var skillSpells = new HashSet<int>(DBC.DBC.SkillLineAbility.Values.Select(s => s.Spell));

            Assert.All(spells, s =>
            {
                Assert.False(string.IsNullOrEmpty(s.Text));
                Assert.False(string.IsNullOrEmpty(s.ToolTip));
                Assert.True(DBC.DBC.SpellInfoStore.ContainsKey(int.Parse(s.Key)),
                    $"child node Key '{s.Key}' is not a spell id");
                Assert.Equal(skillSpells.Contains(int.Parse(s.Key)), s.Kind == ProcTreeNodeKind.Skill);
            });

            // Every spell in the family carries SPELLFAMILY_MAGE by construction.
            Assert.All(spells, s => Assert.Equal((uint)SpellFamilyNames.SPELLFAMILY_MAGE,
                DBC.DBC.SpellInfoStore[int.Parse(s.Key)].SpellFamilyName));
        }

        [SkippableFact]
        public void BuildFamilyTree_attaches_a_probe_spell_under_exactly_its_mask_bits()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            // Pins the parent index, which the previous version of this suite
            // never checked: nodes.SelectMany(n => n.Children) flattens the
            // tree before asserting anything, so it is blind to *which* mask
            // node a spell hangs under. Deleting the ContainsElement filter in
            // ProcInfo.cs, or an off-by-one/reversal in the parent index,
            // both passed the old assertions. Checking both presence (bit
            // set => child present) and absence (bit clear => child absent)
            // across all 96 positions is what catches a deleted filter --
            // presence alone still passes if every spell attaches everywhere.
            var probe = DBC.DBC.SpellInfoStore.Values.FirstOrDefault(s =>
                s.SpellFamilyName == (uint)SpellFamilyNames.SPELLFAMILY_MAGE &&
                s.SpellClassMask.Any(w => w != 0));

            Assert.True(probe != null, "no Mage spell with a non-zero SpellClassMask in this client data");

            var nodes = ProcInfo.BuildFamilyTree(SpellFamilyNames.SPELLFAMILY_MAGE);
            var key = probe.ID.ToString();

            for (var i = 0; i < 96; ++i)
            {
                var expected = (probe.SpellClassMask[i / 32] & (1U << (i % 32))) != 0;
                Assert.Equal(expected, nodes[i].Children.Any(c => c.Key == key));
            }
        }
    }
}
