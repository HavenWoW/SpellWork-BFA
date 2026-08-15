using SpellWork.DBC.Structures;
using System;
using System.Linq;

namespace SpellWork.Spell
{
    public static class ExpectedStat
    {
        public static float Evaluate(ExpectedStatType stat, uint level, int expansion, int contentTuningId, int mythicPlusSeasonId, Classes unitClass)
        {
            var expectedStat = DBC.DBC.ExpectedStat.Values
                .Where(es => es.Lvl == level && (es.ExpansionID == expansion || es.ExpansionID == -2))
                .OrderByDescending(es => es.ExpansionID)
                .FirstOrDefault();

            if (expectedStat == null)
                return 1.0f;

            ExpectedStatModEntry classMod = null;
            switch (unitClass)
            {
                case Classes.CLASS_WARRIOR:
                    classMod = DBC.DBC.ExpectedStatMod[4];
                    break;
                case Classes.CLASS_PALADIN:
                    classMod = DBC.DBC.ExpectedStatMod[2];
                    break;
                case Classes.CLASS_ROGUE:
                    classMod = DBC.DBC.ExpectedStatMod[3];
                    break;
                case Classes.CLASS_MAGE:
                    classMod = DBC.DBC.ExpectedStatMod[1];
                    break;
                default:
                    break;
            }

            var contentTuningMods = DBC.DBC.ContentTuningXExpected.Values
                .Where(ctxe => ctxe.ContentTuningID == contentTuningId && ctxe.MythicPlusSeasonID == mythicPlusSeasonId)
                .Where(ctxe => DBC.DBC.ExpectedStatMod.ContainsKey(ctxe.ExpectedStatModID))
                .Select(ctxe => DBC.DBC.ExpectedStatMod[ctxe.ExpectedStatModID]);

            Func<ExpectedStatModEntry, float> modValueExtractor = null;
            float value = 0.0f;
            switch (stat)
            {
                case ExpectedStatType.CreatureHealth:
                    value = expectedStat.CreatureHealth;
                    modValueExtractor = mod => mod.CreatureHealthMod;
                    break;
                case ExpectedStatType.PlayerHealth:
                    value = expectedStat.PlayerHealth;
                    modValueExtractor = mod => mod.PlayerHealthMod;
                    break;
                case ExpectedStatType.CreatureAutoAttackDps:
                    value = expectedStat.CreatureAutoAttackDps;
                    modValueExtractor = mod => mod.CreatureAutoAttackDPSMod;
                    break;
                case ExpectedStatType.CreatureArmor:
                    value = expectedStat.CreatureArmor;
                    modValueExtractor = mod => mod.CreatureArmorMod;
                    break;
                case ExpectedStatType.PlayerMana:
                    value = expectedStat.PlayerMana;
                    modValueExtractor = mod => mod.PlayerManaMod;
                    break;
                case ExpectedStatType.PlayerPrimaryStat:
                    value = expectedStat.PlayerPrimaryStat;
                    modValueExtractor = mod => mod.PlayerPrimaryStatMod;
                    break;
                case ExpectedStatType.PlayerSecondaryStat:
                    value = expectedStat.PlayerSecondaryStat;
                    modValueExtractor = mod => mod.PlayerSecondaryStatMod;
                    break;
                case ExpectedStatType.ArmorConstant:
                    value = expectedStat.ArmorConstant;
                    modValueExtractor = mod => mod.ArmorConstantMod;
                    break;
                case ExpectedStatType.None:
                    return 0.0f;
                case ExpectedStatType.CreatureSpellDamage:
                    value = expectedStat.CreatureSpellDamage;
                    modValueExtractor = mod => mod.CreatureSpellDamageMod;
                    break;
                default:
                    return 0.0f;
            }

            if (classMod != null)
                value *= modValueExtractor.Invoke(classMod);

            return contentTuningMods.Select(modValueExtractor).Aggregate(value, (a, b) => a * b);
        }

        public static ExpectedStatType GetTypeForSpellEffect(SpellEffectEntry effect)
        {
            switch ((SpellEffects)effect.Effect)
            {
                case SpellEffects.SPELL_EFFECT_SCHOOL_DAMAGE:
                case SpellEffects.SPELL_EFFECT_ENVIRONMENTAL_DAMAGE:
                case SpellEffects.SPELL_EFFECT_HEALTH_LEECH:
                case SpellEffects.SPELL_EFFECT_WEAPON_DAMAGE_NOSCHOOL:
                case SpellEffects.SPELL_EFFECT_WEAPON_DAMAGE:
                    return ExpectedStatType.CreatureSpellDamage;
                case SpellEffects.SPELL_EFFECT_HEAL:
                case SpellEffects.SPELL_EFFECT_HEAL_MECHANICAL:
                    return ExpectedStatType.PlayerHealth;
                case SpellEffects.SPELL_EFFECT_ENERGIZE:
                case SpellEffects.SPELL_EFFECT_POWER_BURN:
                    if (effect.EffectMiscValue[0] == 0)
                        return ExpectedStatType.PlayerMana;
                    return ExpectedStatType.None;
                case SpellEffects.SPELL_EFFECT_POWER_DRAIN:
                    return ExpectedStatType.PlayerMana;
                case SpellEffects.SPELL_EFFECT_APPLY_AURA:
                case SpellEffects.SPELL_EFFECT_PERSISTENT_AREA_AURA:
                case SpellEffects.SPELL_EFFECT_APPLY_AREA_AURA_PARTY:
                case SpellEffects.SPELL_EFFECT_APPLY_AREA_AURA_RAID:
                case SpellEffects.SPELL_EFFECT_APPLY_AREA_AURA_PET:
                case SpellEffects.SPELL_EFFECT_APPLY_AREA_AURA_FRIEND:
                case SpellEffects.SPELL_EFFECT_APPLY_AREA_AURA_ENEMY:
                case SpellEffects.SPELL_EFFECT_APPLY_AREA_AURA_OWNER:
                case SpellEffects.SPELL_EFFECT_APPLY_AURA_ON_PET:
                case SpellEffects.SPELL_EFFECT_202:
                case SpellEffects.SPELL_EFFECT_APPLY_AREA_AURA_PARTY_NONRANDOM:
                    switch ((AuraType)effect.EffectAura)
                    {
                        case AuraType.SPELL_AURA_PERIODIC_DAMAGE:
                        case AuraType.SPELL_AURA_MOD_DAMAGE_DONE:
                        case AuraType.SPELL_AURA_DAMAGE_SHIELD:
                        case AuraType.SPELL_AURA_PROC_TRIGGER_DAMAGE:
                        case AuraType.SPELL_AURA_PERIODIC_LEECH:
                        case AuraType.SPELL_AURA_MOD_DAMAGE_DONE_CREATURE:
                        case AuraType.SPELL_AURA_PERIODIC_HEALTH_FUNNEL:
                        case AuraType.SPELL_AURA_MOD_MELEE_ATTACK_POWER_VERSUS:
                        case AuraType.SPELL_AURA_MOD_RANGED_ATTACK_POWER_VERSUS:
                        case AuraType.SPELL_AURA_MOD_FLAT_SPELL_DAMAGE_VERSUS:
                            return ExpectedStatType.CreatureSpellDamage;
                        case AuraType.SPELL_AURA_PERIODIC_HEAL:
                        case AuraType.SPELL_AURA_MOD_DAMAGE_TAKEN:
                        case AuraType.SPELL_AURA_MOD_INCREASE_HEALTH:
                        case AuraType.SPELL_AURA_SCHOOL_ABSORB:
                        case AuraType.SPELL_AURA_MOD_REGEN:
                        case AuraType.SPELL_AURA_MANA_SHIELD:
                        case AuraType.SPELL_AURA_MOD_HEALING:
                        case AuraType.SPELL_AURA_MOD_HEALING_DONE:
                        case AuraType.SPELL_AURA_MOD_HEALTH_REGEN_IN_COMBAT:
                        case AuraType.SPELL_AURA_MOD_MAX_HEALTH:
                        case AuraType.SPELL_AURA_MOD_INCREASE_HEALTH_2:
                        case AuraType.SPELL_AURA_SCHOOL_HEAL_ABSORB:
                            return ExpectedStatType.PlayerHealth;
                        case AuraType.SPELL_AURA_PERIODIC_MANA_LEECH:
                            return ExpectedStatType.PlayerMana;
                        case AuraType.SPELL_AURA_MOD_STAT:
                        case AuraType.SPELL_AURA_MOD_ATTACK_POWER:
                        case AuraType.SPELL_AURA_MOD_RANGED_ATTACK_POWER:
                            return ExpectedStatType.PlayerPrimaryStat;
                        case AuraType.SPELL_AURA_MOD_RATING:
                            return ExpectedStatType.PlayerSecondaryStat;
                        case AuraType.SPELL_AURA_MOD_RESISTANCE:
                        case AuraType.SPELL_AURA_MOD_BASE_RESISTANCE:
                        case AuraType.SPELL_AURA_MOD_TARGET_RESISTANCE:
                        case AuraType.SPELL_AURA_MOD_BONUS_ARMOR:
                            return ExpectedStatType.ArmorConstant;
                        case AuraType.SPELL_AURA_PERIODIC_ENERGIZE:
                        case AuraType.SPELL_AURA_MOD_INCREASE_ENERGY:
                        case AuraType.SPELL_AURA_MOD_POWER_COST_SCHOOL:
                        case AuraType.SPELL_AURA_MOD_POWER_REGEN:
                        case AuraType.SPELL_AURA_POWER_BURN:
                        case AuraType.SPELL_AURA_MOD_MAX_POWER:
                            if (effect.EffectMiscValue[0] == 0)
                                return ExpectedStatType.PlayerMana;
                            return ExpectedStatType.None;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }

            return ExpectedStatType.None;
        }
    }
}
