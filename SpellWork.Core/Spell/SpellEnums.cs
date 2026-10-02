using System;

#pragma warning disable CA1712 // Do not prefix enum values with type name

namespace SpellWork.Spell
{
    // ReSharper disable InconsistentNaming
    // Generated for client 5.4.8 (18414). Numeric ids come from the SkyFire_548 server
    // headers (SharedDefines.h, SpellAuraDefines.h, SpellMgr.h, Unit.h, DBCEnums.h);
    // anything those headers do not define comes from upstream SpellWork 5.4.8
    // (SpellWork/Spell/SpellEnums.cs). Comments on members are carried from the source.

    // 5.4.8: SkyFire_548 DBCEnums.h DifficultyID
    public enum Difficulty
    {
        DIFFICULTY_NONE         = 0,
        DIFFICULTY_NORMAL       = 1,
        DIFFICULTY_HEROIC       = 2,
        DIFFICULTY_10MAN_NORMAL = 3,
        DIFFICULTY_25MAN_NORMAL = 4,
        DIFFICULTY_10MAN_HEROIC = 5,
        DIFFICULTY_25MAN_HEROIC = 6,
        DIFFICULTY_25MAN_LFR    = 7,
        DIFFICULTY_CHALLENGE    = 8,
        DIFFICULTY_40MAN        = 9,
        DIFFICULTY_SCE_HEROIC   = 11,
        DIFFICULTY_SCE_NORMAL   = 12,
        DIFFICULTY_FLEX         = 14,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellFamilyNames
    public enum SpellFamilyNames
    {
        SPELLFAMILY_GENERIC     = 0,
        SPELLFAMILY_UNK1        = 1, // events, holidays
        SPELLFAMILY_MAGE        = 3,
        SPELLFAMILY_WARRIOR     = 4,
        SPELLFAMILY_WARLOCK     = 5,
        SPELLFAMILY_PRIEST      = 6,
        SPELLFAMILY_DRUID       = 7,
        SPELLFAMILY_ROGUE       = 8,
        SPELLFAMILY_HUNTER      = 9,
        SPELLFAMILY_PALADIN     = 10,
        SPELLFAMILY_SHAMAN      = 11,
        SPELLFAMILY_UNK2        = 12, // 2 spells (silence resistance)
        SPELLFAMILY_POTION      = 13,
        SPELLFAMILY_DEATHKNIGHT = 15,
        SPELLFAMILY_PET         = 17,
        SPELLFAMILY_TOTEMS      = 50,
        SPELLFAMILY_MONK        = 53,
        SPELLFAMILY_WARLOCK_PET = 57,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellEffects
    public enum SpellEffects
    {
        SPELL_EFFECT_INSTAKILL                        = 1,
        SPELL_EFFECT_SCHOOL_DAMAGE                    = 2,
        SPELL_EFFECT_DUMMY                            = 3,
        SPELL_EFFECT_PORTAL_TELEPORT                  = 4, // Unused (4.3.4)
        SPELL_EFFECT_TELEPORT_UNITS                   = 5,
        SPELL_EFFECT_APPLY_AURA                       = 6,
        SPELL_EFFECT_ENVIRONMENTAL_DAMAGE             = 7,
        SPELL_EFFECT_POWER_DRAIN                      = 8,
        SPELL_EFFECT_HEALTH_LEECH                     = 9,
        SPELL_EFFECT_HEAL                             = 10,
        SPELL_EFFECT_BIND                             = 11,
        SPELL_EFFECT_PORTAL                           = 12,
        SPELL_EFFECT_RITUAL_BASE                      = 13, // Unused (4.3.4)
        SPELL_EFFECT_RITUAL_SPECIALIZE                = 14, // Unused (4.3.4)
        SPELL_EFFECT_RITUAL_ACTIVATE_PORTAL           = 15, // Unused (4.3.4)
        SPELL_EFFECT_QUEST_COMPLETE                   = 16,
        SPELL_EFFECT_WEAPON_DAMAGE_NOSCHOOL           = 17,
        SPELL_EFFECT_RESURRECT                        = 18,
        SPELL_EFFECT_ADD_EXTRA_ATTACKS                = 19,
        SPELL_EFFECT_DODGE                            = 20,
        SPELL_EFFECT_EVADE                            = 21,
        SPELL_EFFECT_PARRY                            = 22,
        SPELL_EFFECT_BLOCK                            = 23,
        SPELL_EFFECT_CREATE_ITEM                      = 24,
        SPELL_EFFECT_WEAPON                           = 25,
        SPELL_EFFECT_DEFENSE                          = 26,
        SPELL_EFFECT_PERSISTENT_AREA_AURA             = 27,
        SPELL_EFFECT_SUMMON                           = 28,
        SPELL_EFFECT_LEAP                             = 29,
        SPELL_EFFECT_ENERGIZE                         = 30,
        SPELL_EFFECT_WEAPON_PERCENT_DAMAGE            = 31,
        SPELL_EFFECT_TRIGGER_MISSILE                  = 32,
        SPELL_EFFECT_OPEN_LOCK                        = 33,
        SPELL_EFFECT_SUMMON_CHANGE_ITEM               = 34,
        SPELL_EFFECT_APPLY_AREA_AURA_PARTY            = 35,
        SPELL_EFFECT_LEARN_SPELL                      = 36,
        SPELL_EFFECT_SPELL_DEFENSE                    = 37,
        SPELL_EFFECT_DISPEL                           = 38,
        SPELL_EFFECT_LANGUAGE                         = 39,
        SPELL_EFFECT_DUAL_WIELD                       = 40,
        SPELL_EFFECT_JUMP                             = 41,
        SPELL_EFFECT_JUMP_DEST                        = 42,
        SPELL_EFFECT_TELEPORT_UNITS_FACE_CASTER       = 43,
        SPELL_EFFECT_SKILL_STEP                       = 44,
        SPELL_EFFECT_PLAY_MOVIE                       = 45,
        SPELL_EFFECT_SPAWN                            = 46,
        SPELL_EFFECT_TRADE_SKILL                      = 47,
        SPELL_EFFECT_STEALTH                          = 48,
        SPELL_EFFECT_DETECT                           = 49,
        SPELL_EFFECT_TRANS_DOOR                       = 50,
        SPELL_EFFECT_FORCE_CRITICAL_HIT               = 51, // Unused (4.3.4)
        SPELL_EFFECT_GUARANTEE_HIT                    = 52, // Unused (4.3.4)
        SPELL_EFFECT_ENCHANT_ITEM                     = 53,
        SPELL_EFFECT_ENCHANT_ITEM_TEMPORARY           = 54,
        SPELL_EFFECT_TAMECREATURE                     = 55,
        SPELL_EFFECT_SUMMON_PET                       = 56,
        SPELL_EFFECT_LEARN_PET_SPELL                  = 57,
        SPELL_EFFECT_WEAPON_DAMAGE                    = 58,
        SPELL_EFFECT_CREATE_RANDOM_ITEM               = 59,
        SPELL_EFFECT_PROFICIENCY                      = 60,
        SPELL_EFFECT_SEND_EVENT                       = 61,
        SPELL_EFFECT_POWER_BURN                       = 62,
        SPELL_EFFECT_THREAT                           = 63,
        SPELL_EFFECT_TRIGGER_SPELL                    = 64,
        SPELL_EFFECT_APPLY_AREA_AURA_RAID             = 65,
        SPELL_EFFECT_CREATE_MANA_GEM                  = 66,
        SPELL_EFFECT_HEAL_MAX_HEALTH                  = 67,
        SPELL_EFFECT_INTERRUPT_CAST                   = 68,
        SPELL_EFFECT_DISTRACT                         = 69,
        SPELL_EFFECT_PULL                             = 70,
        SPELL_EFFECT_PICKPOCKET                       = 71,
        SPELL_EFFECT_ADD_FARSIGHT                     = 72,
        SPELL_EFFECT_UNTRAIN_TALENTS                  = 73,
        SPELL_EFFECT_APPLY_GLYPH                      = 74,
        SPELL_EFFECT_HEAL_MECHANICAL                  = 75,
        SPELL_EFFECT_SUMMON_OBJECT_WILD               = 76,
        SPELL_EFFECT_SCRIPT_EFFECT                    = 77,
        SPELL_EFFECT_ATTACK                           = 78,
        SPELL_EFFECT_SANCTUARY                        = 79,
        SPELL_EFFECT_ADD_COMBO_POINTS                 = 80,
        SPELL_EFFECT_CREATE_HOUSE                     = 81,
        SPELL_EFFECT_BIND_SIGHT                       = 82,
        SPELL_EFFECT_DUEL                             = 83,
        SPELL_EFFECT_STUCK                            = 84,
        SPELL_EFFECT_SUMMON_PLAYER                    = 85,
        SPELL_EFFECT_ACTIVATE_OBJECT                  = 86,
        SPELL_EFFECT_GAMEOBJECT_DAMAGE                = 87,
        SPELL_EFFECT_GAMEOBJECT_REPAIR                = 88,
        SPELL_EFFECT_GAMEOBJECT_SET_DESTRUCTION_STATE = 89,
        SPELL_EFFECT_KILL_CREDIT                      = 90,
        SPELL_EFFECT_THREAT_ALL                       = 91,
        SPELL_EFFECT_ENCHANT_HELD_ITEM                = 92,
        SPELL_EFFECT_FORCE_DESELECT                   = 93,
        SPELL_EFFECT_SELF_RESURRECT                   = 94,
        SPELL_EFFECT_SKINNING                         = 95,
        SPELL_EFFECT_CHARGE                           = 96,
        SPELL_EFFECT_CAST_BUTTON                      = 97,
        SPELL_EFFECT_KNOCK_BACK                       = 98,
        SPELL_EFFECT_DISENCHANT                       = 99,
        SPELL_EFFECT_INEBRIATE                        = 100,
        SPELL_EFFECT_FEED_PET                         = 101,
        SPELL_EFFECT_DISMISS_PET                      = 102,
        SPELL_EFFECT_REPUTATION                       = 103,
        SPELL_EFFECT_SUMMON_OBJECT_SLOT1              = 104,
        SPELL_EFFECT_SUMMON_OBJECT_SLOT2              = 105,
        SPELL_EFFECT_SUMMON_OBJECT_SLOT3              = 106,
        SPELL_EFFECT_SUMMON_OBJECT_SLOT4              = 107,
        SPELL_EFFECT_DISPEL_MECHANIC                  = 108,
        SPELL_EFFECT_RESURRECT_PET                    = 109,
        SPELL_EFFECT_DESTROY_ALL_TOTEMS               = 110,
        SPELL_EFFECT_DURABILITY_DAMAGE                = 111,
        SPELL_EFFECT_112                              = 112,
        SPELL_EFFECT_RESURRECT_NEW                    = 113,
        SPELL_EFFECT_ATTACK_ME                        = 114,
        SPELL_EFFECT_DURABILITY_DAMAGE_PCT            = 115,
        SPELL_EFFECT_SKIN_PLAYER_CORPSE               = 116,
        SPELL_EFFECT_SPIRIT_HEAL                      = 117,
        SPELL_EFFECT_SKILL                            = 118,
        SPELL_EFFECT_APPLY_AREA_AURA_PET              = 119,
        SPELL_EFFECT_TELEPORT_GRAVEYARD               = 120,
        SPELL_EFFECT_NORMALIZED_WEAPON_DMG            = 121,
        SPELL_EFFECT_122                              = 122, // Unused (4.3.4)
        SPELL_EFFECT_SEND_TAXI                        = 123,
        SPELL_EFFECT_PULL_TOWARDS                     = 124,
        SPELL_EFFECT_MODIFY_THREAT_PERCENT            = 125,
        SPELL_EFFECT_STEAL_BENEFICIAL_BUFF            = 126,
        SPELL_EFFECT_PROSPECTING                      = 127,
        SPELL_EFFECT_APPLY_AREA_AURA_FRIEND           = 128,
        SPELL_EFFECT_APPLY_AREA_AURA_ENEMY            = 129,
        SPELL_EFFECT_REDIRECT_THREAT                  = 130,
        SPELL_EFFECT_PLAY_SOUND                       = 131,
        SPELL_EFFECT_PLAY_MUSIC                       = 132,
        SPELL_EFFECT_UNLEARN_SPECIALIZATION           = 133,
        SPELL_EFFECT_KILL_CREDIT2                     = 134,
        SPELL_EFFECT_CALL_PET                         = 135,
        SPELL_EFFECT_HEAL_PCT                         = 136,
        SPELL_EFFECT_ENERGIZE_PCT                     = 137,
        SPELL_EFFECT_LEAP_BACK                        = 138,
        SPELL_EFFECT_CLEAR_QUEST                      = 139,
        SPELL_EFFECT_FORCE_CAST                       = 140,
        SPELL_EFFECT_FORCE_CAST_WITH_VALUE            = 141,
        SPELL_EFFECT_TRIGGER_SPELL_WITH_VALUE         = 142,
        SPELL_EFFECT_APPLY_AREA_AURA_OWNER            = 143,
        SPELL_EFFECT_KNOCK_BACK_DEST                  = 144,
        SPELL_EFFECT_PULL_TOWARDS_DEST                = 145,
        SPELL_EFFECT_ACTIVATE_RUNE                    = 146,
        SPELL_EFFECT_QUEST_FAIL                       = 147,
        SPELL_EFFECT_TRIGGER_MISSILE_SPELL_WITH_VALUE = 148,
        SPELL_EFFECT_CHARGE_DEST                      = 149,
        SPELL_EFFECT_QUEST_START                      = 150,
        SPELL_EFFECT_TRIGGER_SPELL_2                  = 151,
        SPELL_EFFECT_SUMMON_RAF_FRIEND                = 152,
        SPELL_EFFECT_CREATE_TAMED_PET                 = 153,
        SPELL_EFFECT_DISCOVER_TAXI                    = 154,
        SPELL_EFFECT_TITAN_GRIP                       = 155,
        SPELL_EFFECT_ENCHANT_ITEM_PRISMATIC           = 156,
        SPELL_EFFECT_CREATE_ITEM_2                    = 157,
        SPELL_EFFECT_MILLING                          = 158,
        SPELL_EFFECT_ALLOW_RENAME_PET                 = 159,
        SPELL_EFFECT_160                              = 160,
        SPELL_EFFECT_TALENT_SPEC_COUNT                = 161,
        SPELL_EFFECT_TALENT_SPEC_SELECT               = 162,
        SPELL_EFFECT_163                              = 163, // Unused (4.3.4)
        SPELL_EFFECT_REMOVE_AURA                      = 164,
        SPELL_EFFECT_DAMAGE_FROM_MAX_HEALTH_PCT       = 165,
        SPELL_EFFECT_GIVE_CURRENCY                    = 166,
        SPELL_EFFECT_167                              = 167,
        SPELL_EFFECT_168                              = 168,
        SPELL_EFFECT_DESTROY_ITEM                     = 169,
        SPELL_EFFECT_170                              = 170,
        SPELL_EFFECT_171                              = 171, // Summons gamebject
        SPELL_EFFECT_RESURRECT_WITH_AURA              = 172,
        SPELL_EFFECT_UNLOCK_GUILD_VAULT_TAB           = 173, // Guild tab unlocked (guild perk)
        SPELL_EFFECT_174                              = 174,
        SPELL_EFFECT_175                              = 175, // Unused (4.3.4)
        SPELL_EFFECT_176                              = 176, // Some kind of sanctuary effect (Vanish)
        SPELL_EFFECT_177                              = 177,
        SPELL_EFFECT_178                              = 178, // Unused (4.3.4)
        SPELL_EFFECT_CREATE_AREATRIGGER               = 179,
        SPELL_EFFECT_180                              = 180, // Unused (4.3.4)
        SPELL_EFFECT_REMOVE_TALENT                    = 181, // Eg: Tome of the Clear Mind
        SPELL_EFFECT_182                              = 182,
        UNK_183                                       = 183,
        UNK_184                                       = 184,
        UNK_185                                       = 185,
        UNK_186                                       = 186,
        UNK_187                                       = 187,
        UNK_188                                       = 188,
        UNK_189                                       = 189,
        UNK_190                                       = 190,
        UNK_191                                       = 191,
        UNK_192                                       = 192,
        UNK_193                                       = 193,
        UNK_194                                       = 194,
        UNK_195                                       = 195,
        UNK_196                                       = 196,
        UNK_197                                       = 197,
        UNK_198                                       = 198,
        UNK_199                                       = 199,
        SPELL_EFFECT_HEAL_BATTLEPET_PCT               = 200,
        SPELL_EFFECT_BATTLE_PET_UNLOCK                = 201,
        UNK_202                                       = 202,
        UNK_203                                       = 203,
        UNK_204                                       = 204,
        UNK_205                                       = 205,
        UNK_206                                       = 206,
        UNK_207                                       = 207,
        UNK_208                                       = 208,
        UNK_209                                       = 209,
        UNK_210                                       = 210,
        UNK_211                                       = 211,
        UNK_212                                       = 212,
        UNK_213                                       = 213,
        TOTAL_SPELL_EFFECTS                           = 214,
    }

    // 5.4.8: SkyFire_548 SpellAuraDefines.h AuraType
    public enum AuraType
    {
        SPELL_AURA_NONE                                      = 0,
        SPELL_AURA_BIND_SIGHT                                = 1,
        SPELL_AURA_MOD_POSSESS                               = 2,
        SPELL_AURA_PERIODIC_DAMAGE                           = 3,
        SPELL_AURA_DUMMY                                     = 4,
        SPELL_AURA_MOD_CONFUSE                               = 5,
        SPELL_AURA_MOD_CHARM                                 = 6,
        SPELL_AURA_MOD_FEAR                                  = 7,
        SPELL_AURA_PERIODIC_HEAL                             = 8,
        SPELL_AURA_MOD_ATTACKSPEED                           = 9,
        SPELL_AURA_MOD_THREAT                                = 10,
        SPELL_AURA_MOD_TAUNT                                 = 11,
        SPELL_AURA_MOD_STUN                                  = 12,
        SPELL_AURA_MOD_DAMAGE_DONE                           = 13,
        SPELL_AURA_MOD_DAMAGE_TAKEN                          = 14,
        SPELL_AURA_DAMAGE_SHIELD                             = 15,
        SPELL_AURA_MOD_STEALTH                               = 16,
        SPELL_AURA_MOD_STEALTH_DETECT                        = 17,
        SPELL_AURA_MOD_INVISIBILITY                          = 18,
        SPELL_AURA_MOD_INVISIBILITY_DETECT                   = 19,
        SPELL_AURA_OBS_MOD_HEALTH                            = 20, // 20, 21 unofficial
        SPELL_AURA_OBS_MOD_POWER                             = 21,
        SPELL_AURA_MOD_RESISTANCE                            = 22,
        SPELL_AURA_PERIODIC_TRIGGER_SPELL                    = 23,
        SPELL_AURA_PERIODIC_ENERGIZE                         = 24,
        SPELL_AURA_MOD_PACIFY                                = 25,
        SPELL_AURA_MOD_ROOT                                  = 26,
        SPELL_AURA_MOD_SILENCE                               = 27,
        SPELL_AURA_REFLECT_SPELLS                            = 28,
        SPELL_AURA_MOD_STAT                                  = 29,
        SPELL_AURA_MOD_SKILL                                 = 30,
        SPELL_AURA_MOD_INCREASE_SPEED                        = 31,
        SPELL_AURA_MOD_INCREASE_MOUNTED_SPEED                = 32,
        SPELL_AURA_MOD_DECREASE_SPEED                        = 33,
        SPELL_AURA_MOD_INCREASE_HEALTH                       = 34,
        SPELL_AURA_MOD_INCREASE_MAX_POWER_FLAT               = 35,
        SPELL_AURA_MOD_SHAPESHIFT                            = 36,
        SPELL_AURA_EFFECT_IMMUNITY                           = 37,
        SPELL_AURA_STATE_IMMUNITY                            = 38,
        SPELL_AURA_SCHOOL_IMMUNITY                           = 39,
        SPELL_AURA_DAMAGE_IMMUNITY                           = 40,
        SPELL_AURA_DISPEL_IMMUNITY                           = 41,
        SPELL_AURA_PROC_TRIGGER_SPELL                        = 42,
        SPELL_AURA_PROC_TRIGGER_DAMAGE                       = 43,
        SPELL_AURA_TRACK_CREATURES                           = 44,
        SPELL_AURA_TRACK_RESOURCES                           = 45,
        SPELL_AURA_46                                        = 46, // Ignore all Gear test spells
        SPELL_AURA_MOD_PARRY_PERCENT                         = 47,
        SPELL_AURA_48                                        = 48, // One periodic spell
        SPELL_AURA_MOD_DODGE_PERCENT                         = 49,
        SPELL_AURA_MOD_CRITICAL_HEALING_AMOUNT               = 50,
        SPELL_AURA_MOD_BLOCK_PERCENT                         = 51,
        SPELL_AURA_MOD_WEAPON_CRIT_PERCENT                   = 52,
        SPELL_AURA_PERIODIC_LEECH                            = 53,
        SPELL_AURA_MOD_HIT_CHANCE                            = 54,
        SPELL_AURA_MOD_SPELL_HIT_CHANCE                      = 55,
        SPELL_AURA_TRANSFORM                                 = 56,
        SPELL_AURA_MOD_SPELL_CRIT_CHANCE                     = 57,
        SPELL_AURA_MOD_INCREASE_SWIM_SPEED                   = 58,
        SPELL_AURA_MOD_DAMAGE_DONE_CREATURE                  = 59,
        SPELL_AURA_MOD_PACIFY_SILENCE                        = 60,
        SPELL_AURA_MOD_SCALE                                 = 61,
        SPELL_AURA_PERIODIC_HEALTH_FUNNEL                    = 62,
        SPELL_AURA_63                                        = 63, // old SPELL_AURA_PERIODIC_MANA_FUNNEL
        SPELL_AURA_PERIODIC_MANA_LEECH                       = 64,
        SPELL_AURA_MOD_CASTING_SPEED_NOT_STACK               = 65,
        SPELL_AURA_FEIGN_DEATH                               = 66,
        SPELL_AURA_MOD_DISARM                                = 67,
        SPELL_AURA_MOD_STALKED                               = 68,
        SPELL_AURA_SCHOOL_ABSORB                             = 69,
        SPELL_AURA_EXTRA_ATTACKS                             = 70,
        SPELL_AURA_71                                        = 71, // old SPELL_AURA_MOD_SPELL_CRIT_CHANCE_SCHOOL. unused  5.4.8
        SPELL_AURA_MOD_POWER_COST_SCHOOL_PCT                 = 72,
        SPELL_AURA_MOD_POWER_COST_SCHOOL                     = 73,
        SPELL_AURA_REFLECT_SPELLS_SCHOOL                     = 74,
        SPELL_AURA_MOD_LANGUAGE                              = 75,
        SPELL_AURA_FAR_SIGHT                                 = 76,
        SPELL_AURA_MECHANIC_IMMUNITY                         = 77,
        SPELL_AURA_MOUNTED                                   = 78,
        SPELL_AURA_MOD_DAMAGE_PERCENT_DONE                   = 79,
        SPELL_AURA_MOD_PERCENT_STAT                          = 80,
        SPELL_AURA_SPLIT_DAMAGE_PCT                          = 81,
        SPELL_AURA_WATER_BREATHING                           = 82,
        SPELL_AURA_MOD_BASE_RESISTANCE                       = 83,
        SPELL_AURA_MOD_REGEN                                 = 84,
        SPELL_AURA_MOD_POWER_REGEN                           = 85,
        SPELL_AURA_CHANNEL_DEATH_ITEM                        = 86,
        SPELL_AURA_MOD_DAMAGE_PERCENT_TAKEN                  = 87,
        SPELL_AURA_MOD_HEALTH_REGEN_PERCENT                  = 88,
        SPELL_AURA_PERIODIC_DAMAGE_PERCENT                   = 89,
        SPELL_AURA_90                                        = 90, // old SPELL_AURA_MOD_RESIST_CHANCE
        SPELL_AURA_MOD_DETECT_RANGE                          = 91,
        SPELL_AURA_PREVENTS_FLEEING                          = 92,
        SPELL_AURA_MOD_UNATTACKABLE                          = 93,
        SPELL_AURA_INTERRUPT_REGEN                           = 94,
        SPELL_AURA_GHOST                                     = 95,
        SPELL_AURA_SPELL_MAGNET                              = 96,
        SPELL_AURA_MANA_SHIELD                               = 97,
        SPELL_AURA_MOD_SKILL_TALENT                          = 98,
        SPELL_AURA_MOD_ATTACK_POWER                          = 99,
        SPELL_AURA_AURAS_VISIBLE                             = 100,
        SPELL_AURA_MOD_RESISTANCE_PCT                        = 101,
        SPELL_AURA_MOD_MELEE_ATTACK_POWER_VERSUS             = 102,
        SPELL_AURA_MOD_TOTAL_THREAT                          = 103,
        SPELL_AURA_WATER_WALK                                = 104,
        SPELL_AURA_FEATHER_FALL                              = 105,
        SPELL_AURA_HOVER                                     = 106,
        SPELL_AURA_ADD_FLAT_MODIFIER                         = 107,
        SPELL_AURA_ADD_PCT_MODIFIER                          = 108,
        SPELL_AURA_ADD_TARGET_TRIGGER                        = 109,
        SPELL_AURA_MOD_POWER_REGEN_PERCENT                   = 110,
        SPELL_AURA_ADD_CASTER_HIT_TRIGGER                    = 111,
        SPELL_AURA_OVERRIDE_CLASS_SCRIPTS                    = 112,
        SPELL_AURA_MOD_RANGED_DAMAGE_TAKEN                   = 113,
        SPELL_AURA_MOD_RANGED_DAMAGE_TAKEN_PCT               = 114,
        SPELL_AURA_MOD_HEALING                               = 115,
        SPELL_AURA_MOD_REGEN_DURING_COMBAT                   = 116,
        SPELL_AURA_MOD_MECHANIC_RESISTANCE                   = 117,
        SPELL_AURA_MOD_HEALING_PCT                           = 118,
        SPELL_AURA_119                                       = 119, // old SPELL_AURA_SHARE_PET_TRACKING
        SPELL_AURA_UNTRACKABLE                               = 120,
        SPELL_AURA_EMPATHY                                   = 121,
        SPELL_AURA_MOD_OFFHAND_DAMAGE_PCT                    = 122,
        SPELL_AURA_MOD_TARGET_RESISTANCE                     = 123,
        SPELL_AURA_MOD_RANGED_ATTACK_POWER                   = 124,
        SPELL_AURA_MOD_MELEE_DAMAGE_TAKEN                    = 125,
        SPELL_AURA_MOD_MELEE_DAMAGE_TAKEN_PCT                = 126,
        SPELL_AURA_RANGED_ATTACK_POWER_ATTACKER_BONUS        = 127,
        SPELL_AURA_MOD_POSSESS_PET                           = 128,
        SPELL_AURA_MOD_SPEED_ALWAYS                          = 129,
        SPELL_AURA_MOD_MOUNTED_SPEED_ALWAYS                  = 130,
        SPELL_AURA_MOD_RANGED_ATTACK_POWER_VERSUS            = 131,
        SPELL_AURA_MOD_INCREASE_ENERGY_PERCENT               = 132,
        SPELL_AURA_MOD_INCREASE_HEALTH_PERCENT               = 133,
        SPELL_AURA_MOD_MANA_REGEN_INTERRUPT                  = 134,
        SPELL_AURA_MOD_HEALING_DONE                          = 135,
        SPELL_AURA_MOD_HEALING_DONE_PERCENT                  = 136,
        SPELL_AURA_MOD_TOTAL_STAT_PERCENTAGE                 = 137,
        SPELL_AURA_MOD_MELEE_HASTE                           = 138,
        SPELL_AURA_FORCE_REACTION                            = 139,
        SPELL_AURA_MOD_RANGED_HASTE                          = 140,
        SPELL_AURA_141                                       = 141, // old SPELL_AURA_MOD_RANGED_AMMO_HASTE, unused now
        SPELL_AURA_MOD_BASE_RESISTANCE_PCT                   = 142,
        SPELL_AURA_MOD_RESISTANCE_EXCLUSIVE                  = 143,
        SPELL_AURA_SAFE_FALL                                 = 144,
        SPELL_AURA_MOD_PET_TALENT_POINTS                     = 145,
        SPELL_AURA_ALLOW_TAME_PET_TYPE                       = 146,
        SPELL_AURA_MECHANIC_IMMUNITY_MASK                    = 147,
        SPELL_AURA_RETAIN_COMBO_POINTS                       = 148,
        SPELL_AURA_REDUCE_PUSHBACK                           = 149, // Reduce Pushback
        SPELL_AURA_MOD_SHIELD_BLOCKVALUE_PCT                 = 150,
        SPELL_AURA_TRACK_STEALTHED                           = 151, // Track Stealthed
        SPELL_AURA_MOD_DETECTED_RANGE                        = 152, // Mod Detected Range
        SPELL_AURA_153                                       = 153, // old SPELL_AURA_SPLIT_DAMAGE_FLAT. unused 4.3.4
        SPELL_AURA_MOD_STEALTH_LEVEL                         = 154, // Stealth Level Modifier
        SPELL_AURA_155                                       = 155, // old SPELL_AURA_MOD_WATER_BREATHING. unused 5.4.8
        SPELL_AURA_MOD_REPUTATION_GAIN                       = 156, // Mod Reputation Gain
        SPELL_AURA_157                                       = 157, // old SPELL_AURA_PET_DAMAGE_MULTI. unused 5.4.8
        SPELL_AURA_MOD_SHIELD_BLOCKVALUE                     = 158,
        SPELL_AURA_NO_PVP_CREDIT                             = 159,
        SPELL_AURA_160                                       = 160, // old SPELL_AURA_MOD_AOE_AVOIDANCE. Unused 4.3.4
        SPELL_AURA_MOD_HEALTH_REGEN_IN_COMBAT                = 161,
        SPELL_AURA_POWER_BURN                                = 162,
        SPELL_AURA_MOD_CRIT_DAMAGE_BONUS                     = 163,
        SPELL_AURA_164                                       = 164,
        SPELL_AURA_MELEE_ATTACK_POWER_ATTACKER_BONUS         = 165,
        SPELL_AURA_MOD_ATTACK_POWER_PCT                      = 166,
        SPELL_AURA_MOD_RANGED_ATTACK_POWER_PCT               = 167,
        SPELL_AURA_MOD_DAMAGE_DONE_VERSUS                    = 168,
        SPELL_AURA_169                                       = 169, // old SPELL_AURA_MOD_CRIT_PERCENT_VERSUS. unused 4.3.4
        SPELL_AURA_DETECT_AMORE                              = 170,
        SPELL_AURA_MOD_SPEED_NOT_STACK                       = 171,
        SPELL_AURA_MOD_MOUNTED_SPEED_NOT_STACK               = 172,
        SPELL_AURA_173                                       = 173, // old SPELL_AURA_ALLOW_CHAMPION_SPELLS
        SPELL_AURA_MOD_SPELL_DAMAGE_OF_STAT_PERCENT          = 174, // by defeult intelect, dependent from SPELL_AURA_MOD_SPELL_HEALING_OF_STAT_PERCENT
        SPELL_AURA_MOD_SPELL_HEALING_OF_STAT_PERCENT         = 175,
        SPELL_AURA_SPIRIT_OF_REDEMPTION                      = 176,
        SPELL_AURA_AOE_CHARM                                 = 177,
        SPELL_AURA_178                                       = 178, // old SPELL_AURA_MOD_DEBUFF_RESISTANCE, unused
        SPELL_AURA_MOD_ATTACKER_SPELL_CRIT_CHANCE            = 179,
        SPELL_AURA_MOD_FLAT_SPELL_DAMAGE_VERSUS              = 180,
        SPELL_AURA_181                                       = 181, // old SPELL_AURA_MOD_FLAT_SPELL_CRIT_DAMAGE_VERSUS - possible flat spell crit damage versus
        SPELL_AURA_182                                       = 182, // old SPELL_AURA_MOD_RESISTANCE_OF_STAT_PERCENT. unused 5.4.8
        SPELL_AURA_MOD_CRITICAL_THREAT                       = 183,
        SPELL_AURA_MOD_ATTACKER_MELEE_HIT_CHANCE             = 184,
        SPELL_AURA_MOD_ATTACKER_RANGED_HIT_CHANCE            = 185,
        SPELL_AURA_MOD_ATTACKER_SPELL_HIT_CHANCE             = 186,
        SPELL_AURA_MOD_ATTACKER_MELEE_CRIT_CHANCE            = 187,
        SPELL_AURA_188                                       = 188, // old SPELL_AURA_MOD_ATTACKER_RANGED_CRIT_CHANCE. unused 5.4.8
        SPELL_AURA_MOD_RATING                                = 189,
        SPELL_AURA_MOD_FACTION_REPUTATION_GAIN               = 190,
        SPELL_AURA_USE_NORMAL_MOVEMENT_SPEED                 = 191,
        SPELL_AURA_MOD_MELEE_RANGED_HASTE                    = 192,
        SPELL_AURA_MELEE_SLOW                                = 193,
        SPELL_AURA_MOD_TARGET_ABSORB_SCHOOL                  = 194,
        SPELL_AURA_MOD_TARGET_ABILITY_ABSORB_SCHOOL          = 195,
        SPELL_AURA_MOD_COOLDOWN                              = 196, // only 24818 Noxious Breath
        SPELL_AURA_MOD_ATTACKER_SPELL_AND_WEAPON_CRIT_CHANCE = 197,
        SPELL_AURA_198                                       = 198, // old SPELL_AURA_MOD_ALL_WEAPON_SKILLS
        SPELL_AURA_199                                       = 199, // old SPELL_AURA_MOD_INCREASES_SPELL_PCT_TO_HIT. unused 4.3.4
        SPELL_AURA_MOD_XP_PCT                                = 200,
        SPELL_AURA_FLY                                       = 201,
        SPELL_AURA_IGNORE_COMBAT_RESULT                      = 202,
        SPELL_AURA_MOD_ATTACKER_MELEE_CRIT_DAMAGE            = 203,
        SPELL_AURA_MOD_ATTACKER_RANGED_CRIT_DAMAGE           = 204,
        SPELL_AURA_MOD_SCHOOL_CRIT_DMG_TAKEN                 = 205,
        SPELL_AURA_MOD_INCREASE_VEHICLE_FLIGHT_SPEED         = 206,
        SPELL_AURA_MOD_INCREASE_MOUNTED_FLIGHT_SPEED         = 207,
        SPELL_AURA_MOD_INCREASE_FLIGHT_SPEED                 = 208,
        SPELL_AURA_MOD_MOUNTED_FLIGHT_SPEED_ALWAYS           = 209,
        SPELL_AURA_MOD_VEHICLE_SPEED_ALWAYS                  = 210,
        SPELL_AURA_MOD_FLIGHT_SPEED_NOT_STACK                = 211,
        SPELL_AURA_212                                       = 212, // old SPELL_AURA_MOD_RANGED_ATTACK_POWER_OF_STAT_PERCENT. unused 4.3.4
        SPELL_AURA_MOD_RAGE_FROM_DAMAGE_DEALT                = 213,
        SPELL_AURA_214                                       = 214,
        SPELL_AURA_ARENA_PREPARATION                         = 215,
        SPELL_AURA_HASTE_SPELLS                              = 216,
        SPELL_AURA_MOD_MELEE_HASTE_2                         = 217,
        SPELL_AURA_HASTE_RANGED                              = 218,
        SPELL_AURA_219                                       = 219, // old SPELL_AURA_MOD_MANA_REGEN_FROM_STAT. unused 5.4.8
        SPELL_AURA_MOD_RATING_FROM_STAT                      = 220,
        SPELL_AURA_MOD_DETAUNT                               = 221,
        SPELL_AURA_222                                       = 222,
        SPELL_AURA_RAID_PROC_FROM_CHARGE                     = 223,
        SPELL_AURA_224                                       = 224,
        SPELL_AURA_RAID_PROC_FROM_CHARGE_WITH_VALUE          = 225,
        SPELL_AURA_PERIODIC_DUMMY                            = 226,
        SPELL_AURA_PERIODIC_TRIGGER_SPELL_WITH_VALUE         = 227,
        SPELL_AURA_DETECT_STEALTH                            = 228,
        SPELL_AURA_MOD_AOE_DAMAGE_AVOIDANCE                  = 229,
        SPELL_AURA_MOD_MAX_HEALTH                            = 230,
        SPELL_AURA_PROC_TRIGGER_SPELL_WITH_VALUE             = 231,
        SPELL_AURA_MECHANIC_DURATION_MOD                     = 232,
        SPELL_AURA_CHANGE_MODEL_FOR_ALL_HUMANOIDS            = 233, // client-side only
        SPELL_AURA_MECHANIC_DURATION_MOD_NOT_STACK           = 234,
        SPELL_AURA_MOD_DISPEL_RESIST                         = 235,
        SPELL_AURA_CONTROL_VEHICLE                           = 236,
        SPELL_AURA_237                                       = 237, // old SPELL_AURA_MOD_SPELL_DAMAGE_OF_ATTACK_POWER. unused 5.4.8
        SPELL_AURA_238                                       = 238, // old SPELL_AURA_MOD_SPELL_HEALING_OF_ATTACK_POWER. unused 5.4.8
        SPELL_AURA_MOD_SCALE_2                               = 239,
        SPELL_AURA_MOD_EXPERTISE                             = 240,
        SPELL_AURA_FORCE_MOVE_FORWARD                        = 241,
        SPELL_AURA_242                                       = 242, // old SPELL_AURA_MOD_SPELL_DAMAGE_FROM_HEALING. unused 5.4.8
        SPELL_AURA_MOD_FACTION                               = 243,
        SPELL_AURA_COMPREHEND_LANGUAGE                       = 244,
        SPELL_AURA_MOD_AURA_DURATION_BY_DISPEL               = 245,
        SPELL_AURA_246                                       = 246, // old SPELL_AURA_MOD_AURA_DURATION_BY_DISPEL_NOT_STACK. unused 5.4.8
        SPELL_AURA_CLONE_CASTER                              = 247,
        SPELL_AURA_MOD_COMBAT_RESULT_CHANCE                  = 248,
        SPELL_AURA_CONVERT_RUNE                              = 249,
        SPELL_AURA_MOD_INCREASE_HEALTH_2                     = 250,
        SPELL_AURA_MOD_ENEMY_DODGE                           = 251,
        SPELL_AURA_MOD_SPEED_SLOW_ALL                        = 252,
        SPELL_AURA_MOD_BLOCK_CRIT_CHANCE                     = 253,
        SPELL_AURA_MOD_DISARM_OFFHAND                        = 254,
        SPELL_AURA_MOD_MECHANIC_DAMAGE_TAKEN_PERCENT         = 255,
        SPELL_AURA_256                                       = 256, // old SPELL_AURA_NO_REAGENT_USE. unused 5.4.8
        SPELL_AURA_257                                       = 257, // old SPELL_AURA_MOD_TARGET_RESIST_BY_SPELL_CLASS. unused 5.4.8
        SPELL_AURA_258                                       = 258,
        SPELL_AURA_MOD_HOT_PCT                               = 259,
        SPELL_AURA_SCREEN_EFFECT                             = 260,
        SPELL_AURA_PHASE                                     = 261,
        SPELL_AURA_ABILITY_IGNORE_AURASTATE                  = 262,
        SPELL_AURA_ALLOW_ONLY_ABILITY                        = 263,
        SPELL_AURA_264                                       = 264,
        SPELL_AURA_265                                       = 265,
        SPELL_AURA_266                                       = 266,
        SPELL_AURA_MOD_IMMUNE_AURA_APPLY_SCHOOL              = 267,
        SPELL_AURA_268                                       = 268, // old SPELL_AURA_MOD_ATTACK_POWER_OF_STAT_PERCENT. unused 4.3.4
        SPELL_AURA_MOD_IGNORE_TARGET_RESIST                  = 269,
        SPELL_AURA_270                                       = 270, // old SPELL_AURA_MOD_ABILITY_IGNORE_TARGET_RESIST, unused 4.3.4
        SPELL_AURA_MOD_DAMAGE_FROM_CASTER                    = 271,
        SPELL_AURA_IGNORE_MELEE_RESET                        = 272,
        SPELL_AURA_X_RAY                                     = 273,
        SPELL_AURA_274                                       = 274, // old SPELL_AURA_ABILITY_CONSUME_NO_AMMO, unused 4.3.4
        SPELL_AURA_MOD_IGNORE_SHAPESHIFT                     = 275,
        SPELL_AURA_MOD_DAMAGE_DONE_FOR_MECHANIC              = 276,
        SPELL_AURA_277                                       = 277, // old SPELL_AURA_MOD_MAX_AFFECTED_TARGETS. unused 4.3.4
        SPELL_AURA_MOD_DISARM_RANGED                         = 278,
        SPELL_AURA_INITIALIZE_IMAGES                         = 279,
        SPELL_AURA_MOD_ARMOR_PENETRATION_PCT                 = 280, // 5.4.8 (125359 Tiger Power)
        SPELL_AURA_MOD_HONOR_GAIN_PCT                        = 281,
        SPELL_AURA_MOD_BASE_HEALTH_PCT                       = 282,
        SPELL_AURA_MOD_HEALING_RECEIVED                      = 283, // Possibly only for some spell family class spells
        SPELL_AURA_LINKED                                    = 284,
        SPELL_AURA_285                                       = 285, // old SPELL_AURA_MOD_ATTACK_POWER_OF_ARMOR. unused 5.4.8
        SPELL_AURA_ABILITY_PERIODIC_CRIT                     = 286,
        SPELL_AURA_DEFLECT_SPELLS                            = 287,
        SPELL_AURA_IGNORE_HIT_DIRECTION                      = 288,
        SPELL_AURA_289                                       = 289,
        SPELL_AURA_MOD_CRIT_PCT                              = 290,
        SPELL_AURA_MOD_XP_QUEST_PCT                          = 291,
        SPELL_AURA_292                                       = 292, // old SPELL_AURA_OPEN_STABLE. unused 5.4.8
        SPELL_AURA_OVERRIDE_SPELLS                           = 293,
        SPELL_AURA_PREVENT_REGENERATE_POWER                  = 294,
        SPELL_AURA_295                                       = 295,
        SPELL_AURA_SET_VEHICLE_ID                            = 296,
        SPELL_AURA_BLOCK_SPELL_FAMILY                        = 297,
        SPELL_AURA_STRANGULATE                               = 298,
        SPELL_AURA_299                                       = 299,
        SPELL_AURA_SHARE_DAMAGE_PCT                          = 300,
        SPELL_AURA_SCHOOL_HEAL_ABSORB                        = 301,
        SPELL_AURA_302                                       = 302,
        SPELL_AURA_MOD_DAMAGE_DONE_VERSUS_AURASTATE          = 303,
        SPELL_AURA_MOD_FAKE_INEBRIATE                        = 304,
        SPELL_AURA_MOD_MINIMUM_SPEED                         = 305,
        SPELL_AURA_306                                       = 306,
        SPELL_AURA_307                                       = 307,
        SPELL_AURA_MOD_CRIT_CHANCE_FOR_CASTER                = 308,
        SPELL_AURA_309                                       = 309,
        SPELL_AURA_MOD_CREATURE_AOE_DAMAGE_AVOIDANCE         = 310,
        SPELL_AURA_311                                       = 311,
        SPELL_AURA_ANIM_REPLACEMENT_SET                      = 312,
        SPELL_AURA_313                                       = 313, // Not used in 4.3.4 - related to mounts
        SPELL_AURA_PREVENT_RESURRECTION                      = 314,
        SPELL_AURA_UNDERWATER_WALKING                        = 315,
        SPELL_AURA_316                                       = 316,
        SPELL_AURA_MOD_SPELL_POWER_PCT                       = 317,
        SPELL_AURA_MASTERY                                   = 318,
        SPELL_AURA_MOD_MELEE_HASTE_3                         = 319,
        SPELL_AURA_MOD_RANGED_HASTE_2                        = 320,
        SPELL_AURA_321                                       = 321,
        SPELL_AURA_INTERFERE_TARGETTING                      = 322, // NYI
        SPELL_AURA_323                                       = 323, // Not used in 4.3.4
        SPELL_AURA_324                                       = 324, // spell critical chance (probably by school mask)
        SPELL_AURA_325                                       = 325, // Not used in 4.3.4
        SPELL_AURA_PHASE_GROUP                               = 326, // phase related
        SPELL_AURA_327                                       = 327, // Not used in 4.3.4
        SPELL_AURA_PROC_ON_POWER_AMOUNT                      = 328,
        SPELL_AURA_MOD_RUNE_REGEN_SPEED                      = 329, // NYI
        SPELL_AURA_CAST_WHILE_WALKING                        = 330,
        SPELL_AURA_FORCE_WEATHER                             = 331,
        SPELL_AURA_OVERRIDE_ACTIONBAR_SPELLS                 = 332,
        SPELL_AURA_OVERRIDE_ACTIONBAR_SPELLS_2               = 333,
        SPELL_AURA_MOD_BLIND                                 = 334, // NYI
        SPELL_AURA_335                                       = 335,
        SPELL_AURA_MOD_FLYING_RESTRICTIONS                   = 336, // NYI
        SPELL_AURA_MOD_VENDOR_ITEMS_PRICES                   = 337,
        SPELL_AURA_MOD_DURABILITY_LOSS                       = 338,
        SPELL_AURA_INCREASE_SKILL_GAIN_CHANCE                = 339, // NYI
        SPELL_AURA_MOD_RESURRECTED_HEALTH_BY_GUILD_MEMBER    = 340, // Increases health gained when resurrected by a guild member by X
        SPELL_AURA_MOD_SPELL_CATEGORY_COOLDOWN               = 341, // Modifies cooldown of all spells using affected category
        SPELL_AURA_MOD_MELEE_RANGED_HASTE_2                  = 342,
        SPELL_AURA_343                                       = 343,
        SPELL_AURA_MOD_AUTOATTACK_DAMAGE                     = 344, // NYI
        SPELL_AURA_BYPASS_ARMOR_FOR_CASTER                   = 345,
        SPELL_AURA_ENABLE_ALT_POWER                          = 346, // NYI
        SPELL_AURA_MOD_SPELL_COOLDOWN_BY_HASTE               = 347, // NYI
        SPELL_AURA_DEPOSIT_BONUS_MONEY_IN_GUILD_BANK_ON_LOOT = 348, // NYI
        SPELL_AURA_MOD_CURRENCY_GAIN                         = 349,
        SPELL_AURA_MOD_GATHERING_ITEMS_GAINED_PERCENT        = 350, // NYI
        SPELL_AURA_351                                       = 351,
        SPELL_AURA_ENABLE_WORGER_ALTERED_FORM                = 352,
        SPELL_AURA_MOD_CAMOUFLAGE                            = 353, // NYI
        SPELL_AURA_354                                       = 354, // Restoration Shaman mastery - mod healing based on target's health (less = more healing)
        SPELL_AURA_355                                       = 355,
        SPELL_AURA_356                                       = 356, // Arcane Mage mastery - mod damage based on current mana
        SPELL_AURA_ENABLE_BOSS1_UNIT_FRAME                   = 357,
        SPELL_AURA_WORGEN_ALTERED_FORM                       = 358,
        SPELL_AURA_359                                       = 359,
        SPELL_AURA_PROC_TRIGGER_SPELL_COPY                   = 360, // Procs the same spell that caused this proc (Dragonwrath, Tarecgosa's Rest)
        SPELL_AURA_PROC_TRIGGER_SPELL_2                      = 361,
        SPELL_AURA_362                                       = 362, // Not used in 4.3.4
        SPELL_AURA_MOD_NEXT_SPELL                            = 363, // Used by 101601 Throw Totem - causes the client to initialize spell cast with specified spell
        SPELL_AURA_364                                       = 364, // Not used in 4.3.4
        SPELL_AURA_MAX_FAR_CLIP_PLANE                        = 365, // Overrides client's View Distance setting to max("Fair", current_setting) and turns off terrain display
        SPELL_AURA_OVERRIDE_SPELL_POWER_BY_AP_PCT            = 366, // Sets spellpower equal to % of attack power, discarding all other bonuses (from gear and buffs)
        SPELL_AURA_OVERRIDE_AUTOATTACK_WITH_SPELL            = 367, // Use spell instead of an autoattack
        SPELL_AURA_368                                       = 368, // Not used in 4.3.4
        SPELL_AURA_ENABLE_POWER_BAR_TIMER                    = 369,
        SPELL_AURA_SET_FAIR_FAR_CLIP                         = 370, // Overrides client's View Distance setting to max("Fair", current_setting)
        SPELL_AURA_371                                       = 371,
        SPELL_AURA_372                                       = 372,
        SPELL_AURA_373                                       = 373, // SPELL_AURA_ALLOW_ONLY_ABILITY2 ??
        SPELL_AURA_MODIFY_FALL_DAMAGE_PCT                    = 374,
        UNK_375                                              = 375,
        SPELL_AURA_MOD_CURRENCY_GAIN2                        = 376,
        SPELL_AURA_CAST_WHILE_WALKING2                       = 377,
        UNK_378                                              = 378,
        SPELL_AURA_379                                       = 379,
        UNK_380                                              = 380,
        SPELL_AURA_381                                       = 381,
        SPELL_AURA_382                                       = 382,
        SPELL_AURA_IGNORE_SPELL_COOLDOWN                     = 383,
        UNK_384                                              = 384,
        SPELL_AURA_385                                       = 385,
        SPELL_AURA_386                                       = 386,
        SPELL_AURA_387                                       = 387,
        SPELL_AURA_388                                       = 388,
        UNK_389                                              = 389,
        UNK_390                                              = 390,
        UNK_391                                              = 391,
        UNK_392                                              = 392,
        SPELL_AURA_393                                       = 393,
        SPELL_AURA_394                                       = 394,
        SPELL_AURA_395                                       = 395,
        SPELL_AURA_396                                       = 396,
        SPELL_AURA_397                                       = 397,
        SPELL_AURA_398                                       = 398,
        UNK_399                                              = 399,
        SPELL_AURA_MOD_SKILL_2                               = 400,
        SPELL_AURA_401                                       = 401,
        SPELL_AURA_402                                       = 402,
        SPELL_AURA_403                                       = 403,
        SPELL_AURA_OVERRIDE_AP_BY_SPELL_POWER_PCT            = 404, // Sets attack power equal to % of spellpower
        SPELL_AURA_405                                       = 405,
        SPELL_AURA_406                                       = 406,
        SPELL_AURA_MOD_FEAR_2                                = 407,
        SPELL_AURA_408                                       = 408,
        SPELL_AURA_409                                       = 409,
        SPELL_AURA_410                                       = 410,
        SPELL_AURA_MOD_CHARGES                               = 411, // implemented in Player spell charge tracking
        SPELL_AURA_412                                       = 412,
        SPELL_AURA_413                                       = 413,
        SPELL_AURA_414                                       = 414,
        SPELL_AURA_415                                       = 415,
        SPELL_AURA_416                                       = 416,
        SPELL_AURA_417                                       = 417,
        SPELL_AURA_418                                       = 418,
        SPELL_AURA_419                                       = 419,
        SPELL_AURA_MOD_PET_XP_PCT                            = 420, // NYI
        SPELL_AURA_421                                       = 421,
        SPELL_AURA_422                                       = 422,
        SPELL_AURA_423                                       = 423,
        SPELL_AURA_424                                       = 424,
        UNK_425                                              = 425,
        SPELL_AURA_426                                       = 426,
        SPELL_AURA_427                                       = 427,
        SPELL_AURA_428                                       = 428,
        SPELL_AURA_429                                       = 429,
        SPELL_AURA_430                                       = 430,
        SPELL_AURA_431                                       = 431,
        SPELL_AURA_432                                       = 432,
        UNK_433                                              = 433,
        SPELL_AURA_434                                       = 434,
        UNK_435                                              = 435,
        SPELL_AURA_436                                       = 436,
        SPELL_AURA_437                                       = 437,
        TOTAL_AURAS                                          = 438, // 5.4.2
    }

    // 5.4.8: SkyFire_548 SharedDefines.h Targets
    public enum Targets
    {
        TARGET_UNIT_CASTER                   = 1,
        TARGET_UNIT_NEARBY_ENEMY             = 2,
        TARGET_UNIT_NEARBY_PARTY             = 3,
        TARGET_UNIT_NEARBY_ALLY              = 4,
        TARGET_UNIT_PET                      = 5,
        TARGET_UNIT_TARGET_ENEMY             = 6,
        TARGET_UNIT_SRC_AREA_ENTRY           = 7,
        TARGET_UNIT_DEST_AREA_ENTRY          = 8,
        TARGET_DEST_HOME                     = 9,
        TARGET_10                            = 10, // (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        TARGET_UNIT_SRC_AREA_UNK_11          = 11,
        TARGET_12                            = 12, // (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        TARGET_13                            = 13, // (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        TARGET_14                            = 14, // (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        TARGET_UNIT_SRC_AREA_ENEMY           = 15,
        TARGET_UNIT_DEST_AREA_ENEMY          = 16,
        TARGET_DEST_DB                       = 17,
        TARGET_DEST_CASTER                   = 18,
        TARGET_19                            = 19, // (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        TARGET_UNIT_CASTER_AREA_PARTY        = 20,
        TARGET_UNIT_TARGET_ALLY              = 21,
        TARGET_SRC_CASTER                    = 22,
        TARGET_GAMEOBJECT_TARGET             = 23,
        TARGET_UNIT_CONE_ENEMY_24            = 24,
        TARGET_UNIT_TARGET_ANY               = 25,
        TARGET_GAMEOBJECT_ITEM_TARGET        = 26,
        TARGET_UNIT_MASTER                   = 27,
        TARGET_DEST_DYNOBJ_ENEMY             = 28,
        TARGET_DEST_DYNOBJ_ALLY              = 29,
        TARGET_UNIT_SRC_AREA_ALLY            = 30,
        TARGET_UNIT_DEST_AREA_ALLY           = 31,
        TARGET_DEST_CASTER_SUMMON            = 32, // front left, doesn't use radius
        TARGET_UNIT_SRC_AREA_PARTY           = 33,
        TARGET_UNIT_DEST_AREA_PARTY          = 34,
        TARGET_UNIT_TARGET_PARTY             = 35,
        TARGET_DEST_CASTER_UNK_36            = 36,
        TARGET_UNIT_LASTTARGET_AREA_PARTY    = 37,
        TARGET_UNIT_NEARBY_ENTRY             = 38,
        TARGET_DEST_CASTER_FISHING           = 39,
        TARGET_GAMEOBJECT_NEARBY_ENTRY       = 40,
        TARGET_DEST_CASTER_FRONT_RIGHT       = 41,
        TARGET_DEST_CASTER_BACK_RIGHT        = 42,
        TARGET_DEST_CASTER_BACK_LEFT         = 43,
        TARGET_DEST_CASTER_FRONT_LEFT        = 44,
        TARGET_UNIT_TARGET_CHAINHEAL_ALLY    = 45,
        TARGET_DEST_NEARBY_ENTRY             = 46,
        TARGET_DEST_CASTER_FRONT             = 47,
        TARGET_DEST_CASTER_BACK              = 48,
        TARGET_DEST_CASTER_RIGHT             = 49,
        TARGET_DEST_CASTER_LEFT              = 50,
        TARGET_GAMEOBJECT_SRC_AREA           = 51,
        TARGET_GAMEOBJECT_DEST_AREA          = 52,
        TARGET_DEST_TARGET_ENEMY             = 53,
        TARGET_UNIT_CONE_ENEMY_54            = 54,
        TARGET_DEST_CASTER_FRONT_LEAP        = 55, // for a leap spell
        TARGET_UNIT_CASTER_AREA_RAID         = 56,
        TARGET_UNIT_TARGET_RAID              = 57,
        TARGET_UNIT_NEARBY_RAID              = 58,
        TARGET_UNIT_CONE_ALLY                = 59,
        TARGET_UNIT_CONE_ENTRY               = 60,
        TARGET_UNIT_TARGET_AREA_RAID_CLASS   = 61,
        TARGET_UNK_62                        = 62,
        TARGET_DEST_TARGET_ANY               = 63,
        TARGET_DEST_TARGET_FRONT             = 64,
        TARGET_DEST_TARGET_BACK              = 65,
        TARGET_DEST_TARGET_RIGHT             = 66,
        TARGET_DEST_TARGET_LEFT              = 67,
        TARGET_DEST_TARGET_FRONT_RIGHT       = 68,
        TARGET_DEST_TARGET_BACK_RIGHT        = 69,
        TARGET_DEST_TARGET_BACK_LEFT         = 70,
        TARGET_DEST_TARGET_FRONT_LEFT        = 71,
        TARGET_DEST_CASTER_RANDOM            = 72,
        TARGET_DEST_CASTER_RADIUS            = 73,
        TARGET_DEST_TARGET_RANDOM            = 74,
        TARGET_DEST_TARGET_RADIUS            = 75,
        TARGET_DEST_CHANNEL_TARGET           = 76,
        TARGET_UNIT_CHANNEL_TARGET           = 77,
        TARGET_DEST_DEST_FRONT               = 78,
        TARGET_DEST_DEST_BACK                = 79,
        TARGET_DEST_DEST_RIGHT               = 80,
        TARGET_DEST_DEST_LEFT                = 81,
        TARGET_DEST_DEST_FRONT_RIGHT         = 82,
        TARGET_DEST_DEST_BACK_RIGHT          = 83,
        TARGET_DEST_DEST_BACK_LEFT           = 84,
        TARGET_DEST_DEST_FRONT_LEFT          = 85,
        TARGET_DEST_DEST_RANDOM              = 86,
        TARGET_DEST_DEST                     = 87,
        TARGET_DEST_DYNOBJ_NONE              = 88,
        TARGET_DEST_TRAJ                     = 89,
        TARGET_UNIT_TARGET_MINIPET           = 90,
        TARGET_DEST_DEST_RADIUS              = 91,
        TARGET_UNIT_SUMMONER                 = 92,
        TARGET_CORPSE_SRC_AREA_ENEMY         = 93, // NYI
        TARGET_UNIT_VEHICLE                  = 94,
        TARGET_UNIT_TARGET_PASSENGER         = 95,
        TARGET_UNIT_PASSENGER_0              = 96,
        TARGET_UNIT_PASSENGER_1              = 97,
        TARGET_UNIT_PASSENGER_2              = 98,
        TARGET_UNIT_PASSENGER_3              = 99,
        TARGET_UNIT_PASSENGER_4              = 100,
        TARGET_UNIT_PASSENGER_5              = 101,
        TARGET_UNIT_PASSENGER_6              = 102,
        TARGET_UNIT_PASSENGER_7              = 103,
        TARGET_UNIT_CONE_ENEMY_104           = 104,
        TARGET_UNIT_UNK_105                  = 105, // 1 spell
        TARGET_DEST_CHANNEL_CASTER           = 106,
        TARGET_UNK_DEST_AREA_UNK_107         = 107, // not enough info - only generic spells avalible
        TARGET_GAMEOBJECT_CONE               = 108,
        TARGET_109                           = 109, // (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        TARGET_DEST_UNK_110                  = 110, // 1 spell
        TARGET_UNK_111                       = 111,
        TARGET_UNK_112                       = 112,
        TARGET_UNK_113                       = 113,
        TARGET_UNK_114                       = 114,
        TARGET_UNK_115                       = 115,
        TARGET_UNK_116                       = 116,
        TARGET_UNK_117                       = 117,
        TARGET_UNIT_TARGET_PARTY_TARGET_RAID = 118, // Buff Spells, like Arcane Brilliance, Mark of the Wild etc.
        TARGET_UNK_119                       = 119,
        TARGET_UNK_120                       = 120,
        TARGET_UNK_121                       = 121,
        TARGET_UNK_122                       = 122,
        TARGET_UNK_123                       = 123,
        TARGET_UNK_124                       = 124,
        TARGET_UNK_125                       = 125,
        TARGET_UNK_126                       = 126,
        TARGET_UNK_127                       = 127,
        TARGET_UNK_128                       = 128, // Placeholder (commented out in SkyFire_548)
        TARGET_UNK_129                       = 129,
        TARGET_UNK_130                       = 130,
        TARGET_UNK_131                       = 131,
        TARGET_UNK_132                       = 132,
        TARGET_UNK_133                       = 133, // Placeholder (commented out in SkyFire_548)
        TARGET_UNK_134                       = 134, // Placeholder (commented out in SkyFire_548)
        TARGET_UNK_135                       = 135, // Placeholder (commented out in SkyFire_548)
        TARGET_UNK_136                       = 136,
        TARGET_UNK_137                       = 137, // Placeholder (commented out in SkyFire_548)
        TARGET_UNK_138                       = 138,
        TARGET_UNK_139                       = 139,
        TARGET_UNK_140                       = 140,
        TARGET_UNK_141                       = 141,
        TARGET_UNK_142                       = 142,
        TARGET_UNK_143                       = 143,
        TOTAL_SPELL_TARGETS                  = 144,
    }

    // 5.4.8: SkyFire_548 SpellMgr.h ProcFlags
    [Flags]
    public enum ProcFlags
    {
        PROC_FLAG_NONE                            = 0x00000000,
        PROC_FLAG_KILLED                          = 0x00000001, // 00 Killed by agressor - not sure about this flag
        PROC_FLAG_KILL                            = 0x00000002, // 01 Kill target (in most cases need XP/Honor reward)
        PROC_FLAG_DONE_MELEE_AUTO_ATTACK          = 0x00000004, // 02 Done melee auto attack
        PROC_FLAG_TAKEN_MELEE_AUTO_ATTACK         = 0x00000008, // 03 Taken melee auto attack
        PROC_FLAG_DONE_SPELL_MELEE_DMG_CLASS      = 0x00000010, // 04 Done attack by Spell that has dmg class melee
        PROC_FLAG_TAKEN_SPELL_MELEE_DMG_CLASS     = 0x00000020, // 05 Taken attack by Spell that has dmg class melee
        PROC_FLAG_DONE_RANGED_AUTO_ATTACK         = 0x00000040, // 06 Done ranged auto attack
        PROC_FLAG_TAKEN_RANGED_AUTO_ATTACK        = 0x00000080, // 07 Taken ranged auto attack
        PROC_FLAG_DONE_SPELL_RANGED_DMG_CLASS     = 0x00000100, // 08 Done attack by Spell that has dmg class ranged
        PROC_FLAG_TAKEN_SPELL_RANGED_DMG_CLASS    = 0x00000200, // 09 Taken attack by Spell that has dmg class ranged
        PROC_FLAG_DONE_SPELL_NONE_DMG_CLASS_POS   = 0x00000400, // 10 Done positive spell that has dmg class none
        PROC_FLAG_TAKEN_SPELL_NONE_DMG_CLASS_POS  = 0x00000800, // 11 Taken positive spell that has dmg class none
        PROC_FLAG_DONE_SPELL_NONE_DMG_CLASS_NEG   = 0x00001000, // 12 Done negative spell that has dmg class none
        PROC_FLAG_TAKEN_SPELL_NONE_DMG_CLASS_NEG  = 0x00002000, // 13 Taken negative spell that has dmg class none
        PROC_FLAG_DONE_SPELL_MAGIC_DMG_CLASS_POS  = 0x00004000, // 14 Done positive spell that has dmg class magic
        PROC_FLAG_TAKEN_SPELL_MAGIC_DMG_CLASS_POS = 0x00008000, // 15 Taken positive spell that has dmg class magic
        PROC_FLAG_DONE_SPELL_MAGIC_DMG_CLASS_NEG  = 0x00010000, // 16 Done negative spell that has dmg class magic
        PROC_FLAG_TAKEN_SPELL_MAGIC_DMG_CLASS_NEG = 0x00020000, // 17 Taken negative spell that has dmg class magic
        PROC_FLAG_DONE_PERIODIC                   = 0x00040000, // 18 Successful do periodic (damage / healing)
        PROC_FLAG_TAKEN_PERIODIC                  = 0x00080000, // 19 Taken spell periodic (damage / healing)
        PROC_FLAG_TAKEN_DAMAGE                    = 0x00100000, // 20 Taken any damage
        PROC_FLAG_DONE_TRAP_ACTIVATION            = 0x00200000, // 21 On trap activation (possibly needs name change to ON_GAMEOBJECT_CAST or USE)
        PROC_FLAG_DONE_MAINHAND_ATTACK            = 0x00400000, // 22 Done main-hand melee attacks (spell and autoattack)
        PROC_FLAG_DONE_OFFHAND_ATTACK             = 0x00800000, // 23 Done off-hand melee attacks (spell and autoattack)
        PROC_FLAG_DEATH                           = 0x01000000, // 24 Died in any way
        AUTO_ATTACK_PROC_FLAG_MASK                = 0x000000CC,
        MELEE_PROC_FLAG_MASK                      = 0x00C0003C,
        RANGED_PROC_FLAG_MASK                     = 0x000003C0,
        SPELL_PROC_FLAG_MASK                      = 0x0003FF30,
        SPELL_CAST_PROC_FLAG_MASK                 = 0x0023FFF0,
        PERIODIC_PROC_FLAG_MASK                   = 0x000C0000,
        DONE_HIT_PROC_FLAG_MASK                   = 0x00C55554,
        TAKEN_HIT_PROC_FLAG_MASK                  = 0x001AAAA8,
        REQ_SPELL_PHASE_PROC_FLAG_MASK            = 0x00015510,
    }

    // 5.4.8: SkyFire_548 SpellMgr.h ProcFlagsExLegacy
    [Flags]
    public enum ProcFlagsEx
    {
        PROC_EX_NONE                = 0x00000000, // If none can tigger on Hit/Crit only (passive spells MUST defined by SpellFamily flag)
        PROC_EX_NORMAL_HIT          = 0x00000001, // If set only from normal hit (only damage spells)
        PROC_EX_CRITICAL_HIT        = 0x00000002,
        PROC_EX_MISS                = 0x00000004,
        PROC_EX_RESIST              = 0x00000008,
        PROC_EX_DODGE               = 0x00000010,
        PROC_EX_PARRY               = 0x00000020,
        PROC_EX_BLOCK               = 0x00000040,
        PROC_EX_EVADE               = 0x00000080,
        PROC_EX_IMMUNE              = 0x00000100,
        PROC_EX_DEFLECT             = 0x00000200,
        PROC_EX_ABSORB              = 0x00000400,
        PROC_EX_REFLECT             = 0x00000800,
        PROC_EX_INTERRUPT           = 0x00001000, // Melee hit result can be Interrupt (not used)
        PROC_EX_FULL_BLOCK          = 0x00002000, // block al attack damage
        PROC_EX_RESERVED2           = 0x00004000,
        PROC_EX_NOT_ACTIVE_SPELL    = 0x00008000, // Spell mustn't do damage/heal to proc
        PROC_EX_EX_TRIGGER_ALWAYS   = 0x00010000, // If set trigger always no matter of hit result
        PROC_EX_EX_ONE_TIME_TRIGGER = 0x00020000, // If set trigger always but only one time (not implemented yet)
        PROC_EX_ONLY_ACTIVE_SPELL   = 0x00040000, // Spell has to do damage/heal to proc
        PROC_EX_INTERNAL_CANT_PROC  = 0x00800000,
        PROC_EX_INTERNAL_DOT        = 0x01000000,
        PROC_EX_INTERNAL_HOT        = 0x02000000,
        PROC_EX_INTERNAL_TRIGGERED  = 0x04000000,
        PROC_EX_INTERNAL_REQ_FAMILY = 0x08000000,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellSchools
    public enum SpellSchools
    {
        SPELL_SCHOOL_NORMAL = 0,
        SPELL_SCHOOL_HOLY   = 1,
        SPELL_SCHOOL_FIRE   = 2,
        SPELL_SCHOOL_NATURE = 3,
        SPELL_SCHOOL_FROST  = 4,
        SPELL_SCHOOL_SHADOW = 5,
        SPELL_SCHOOL_ARCANE = 6,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellSchoolMask
    [Flags]
    public enum SpellSchoolMask
    {
        SPELL_SCHOOL_MASK_NONE   = 0x00, // not exist
        SPELL_SCHOOL_MASK_NORMAL = 0x01, // PHYSICAL (Armor)
        SPELL_SCHOOL_MASK_HOLY   = 0x02,
        SPELL_SCHOOL_MASK_FIRE   = 0x04,
        SPELL_SCHOOL_MASK_NATURE = 0x08,
        SPELL_SCHOOL_MASK_FROST  = 0x10,
        SPELL_SCHOOL_MASK_SHADOW = 0x20,
        SPELL_SCHOOL_MASK_ARCANE = 0x40,
        SPELL_SCHOOL_MASK_SPELL  = 0x7C,
        SPELL_SCHOOL_MASK_MAGIC  = 0x7E,
        SPELL_SCHOOL_MASK_ALL    = 0x7F,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h Mechanics
    public enum Mechanics
    {
        MECHANIC_NONE            = 0,
        MECHANIC_CHARM           = 1,
        MECHANIC_DISORIENTED     = 2,
        MECHANIC_DISARM          = 3,
        MECHANIC_DISTRACT        = 4,
        MECHANIC_FEAR            = 5,
        MECHANIC_GRIP            = 6,
        MECHANIC_ROOT            = 7,
        MECHANIC_SLOW_ATTACK     = 8,
        MECHANIC_SILENCE         = 9,
        MECHANIC_SLEEP           = 10,
        MECHANIC_SNARE           = 11,
        MECHANIC_STUN            = 12,
        MECHANIC_FREEZE          = 13,
        MECHANIC_KNOCKOUT        = 14,
        MECHANIC_BLEED           = 15,
        MECHANIC_BANDAGE         = 16,
        MECHANIC_POLYMORPH       = 17,
        MECHANIC_BANISH          = 18,
        MECHANIC_SHIELD          = 19,
        MECHANIC_SHACKLE         = 20,
        MECHANIC_MOUNT           = 21,
        MECHANIC_INFECTED        = 22,
        MECHANIC_TURN            = 23,
        MECHANIC_HORROR          = 24,
        MECHANIC_INVULNERABILITY = 25,
        MECHANIC_INTERRUPT       = 26,
        MECHANIC_DAZE            = 27,
        MECHANIC_DISCOVERY       = 28,
        MECHANIC_IMMUNE_SHIELD   = 29, // Divine (Blessing) Shield/Protection and Ice Block
        MECHANIC_SAPPED          = 30,
        MECHANIC_ENRAGED         = 31,
        MECHANIC_WOUNDED         = 32,
        MAX_MECHANIC             = 33,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellDmgClass
    public enum SpellDmgClass
    {
        SPELL_DAMAGE_CLASS_NONE   = 0,
        SPELL_DAMAGE_CLASS_MAGIC  = 1,
        SPELL_DAMAGE_CLASS_MELEE  = 2,
        SPELL_DAMAGE_CLASS_RANGED = 3,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellPreventionType
    [Flags]
    public enum SpellPreventionType
    {
        SPELL_PREVENTION_TYPE_NONE    = 0x00000000,
        SPELL_PREVENTION_TYPE_SILENCE = 0x00000001,
        SPELL_PREVENTION_TYPE_PACIFY  = 0x00000002,
        SPELL_PREVENTION_TYPE_UNK     = 0x00000003, // Only a few spells have this, but most of the should be interruptable.
    }

    // 5.4.8: upstream SpellWork 5.4.8 (SkyFire_548 has no stance-mask enum); widened to ulong to match SpellShapeshift.dbc
    [Flags]
    public enum ShapeshiftFormMask : ulong
    {
        FORM_ALL                = 0xFFFFFFFF,
        FORM_NONE               = 0x00000000,
        FORM_CAT                = 0x00000001,   // 1
        FORM_TREE               = 0x00000002,   // 2
        FORM_TRAVEL             = 0x00000004,   // 3
        FORM_AQUA               = 0x00000008,   // 4
        FORM_BEAR               = 0x00000010,   // 5
        FORM_AMBIENT            = 0x00000020,   // 6
        FORM_GHOUL              = 0x00000040,   // 7
        FORM_DIREBEAR           = 0x00000080,   // 8
        FORM_STEVES_GHOUL       = 0x00000100,   // 9
        FORM_THARONJA_SKELETON  = 0x00000200,   // 10
        FORM_TEST_OF_STRENGTH   = 0x00000400,   // 11
        FORM_BLB_PLAYER         = 0x00000800,   // 12
        FORM_SHADOW_DANCE       = 0x00001000,   // 13
        FORM_CREATUREBEAR       = 0x00002000,   // 14
        FORM_CREATURECAT        = 0x00004000,   // 15
        FORM_GHOSTWOLF          = 0x00008000,   // 16
        FORM_BATTLESTANCE       = 0x00010000,   // 17
        FORM_DEFENSIVESTANCE    = 0x00020000,   // 18
        FORM_BERSERKERSTANCE    = 0x00040000,   // 19
        FORM_TEST               = 0x00080000,   // 20
        FORM_ZOMBIE             = 0x00100000,   // 21
        FORM_METAMORPHOSIS      = 0x00200000,   // 22
        FORM_UNK1               = 0x00400000,   // 23
        FORM_UNK2               = 0x00800000,   // 24
        FORM_UNDEAD             = 0x01000000,   // 25
        FORM_MASTER_ANGLER      = 0x02000000,   // 26
        FORM_FLIGHT_EPIC        = 0x04000000,   // 27
        FORM_SHADOW             = 0x08000000,   // 28
        FORM_FLIGHT             = 0x10000000,   // 29
        FORM_STEALTH            = 0x20000000,   // 30
        FORM_MOONKIN            = 0x40000000,   // 31
        FORM_SPIRITOFREDEMPTION = 0x80000000,   // 32
    }

    // 5.4.8: SkyFire_548 SharedDefines.h DispelType
    public enum DispelType
    {
        DISPEL_NONE         = 0,
        DISPEL_MAGIC        = 1,
        DISPEL_CURSE        = 2,
        DISPEL_DISEASE      = 3,
        DISPEL_POISON       = 4,
        DISPEL_STEALTH      = 5,
        DISPEL_INVISIBILITY = 6,
        DISPEL_ALL          = 7,
        DISPEL_SPE_NPC_ONLY = 8,
        DISPEL_ENRAGE       = 9,
        DISPEL_ZG_TICKET    = 10,
        DESPEL_OLD_UNUSED   = 11,
    }

    // 5.4.8: SkyFire_548 Unit.h SpellModOp
    public enum SpellModOp
    {
        SPELLMOD_DAMAGE                    = 0,
        SPELLMOD_DURATION                  = 1,
        SPELLMOD_THREAT                    = 2,
        SPELLMOD_EFFECT1                   = 3,
        SPELLMOD_CHARGES                   = 4,
        SPELLMOD_RANGE                     = 5,
        SPELLMOD_RADIUS                    = 6,
        SPELLMOD_CRITICAL_CHANCE           = 7,
        SPELLMOD_ALL_EFFECTS               = 8,
        SPELLMOD_NOT_LOSE_CASTING_TIME     = 9,
        SPELLMOD_CASTING_TIME              = 10,
        SPELLMOD_COOLDOWN                  = 11,
        SPELLMOD_EFFECT2                   = 12,
        SPELLMOD_IGNORE_ARMOR              = 13,
        SPELLMOD_COST                      = 14,
        SPELLMOD_CRIT_DAMAGE_BONUS         = 15,
        SPELLMOD_RESIST_MISS_CHANCE        = 16,
        SPELLMOD_JUMP_TARGETS              = 17,
        SPELLMOD_CHANCE_OF_SUCCESS         = 18,
        SPELLMOD_ACTIVATION_TIME           = 19,
        SPELLMOD_DAMAGE_MULTIPLIER         = 20,
        SPELLMOD_GLOBAL_COOLDOWN           = 21,
        SPELLMOD_DOT                       = 22,
        SPELLMOD_EFFECT3                   = 23,
        SPELLMOD_BONUS_MULTIPLIER          = 24,
        SPELLMOD_PROC_PER_MINUTE           = 26,
        SPELLMOD_VALUE_MULTIPLIER          = 27,
        SPELLMOD_RESIST_DISPEL_CHANCE      = 28,
        SPELLMOD_CRIT_DAMAGE_BONUS_2       = 29, // one not used spell
        SPELLMOD_SPELL_COST_REFUND_ON_FAIL = 30,
        SPELLMOD_CHARGES2                  = 31,
        SPELLMOD_EFFECT4                   = 32,
        SPELLMOD_EFFECT5                   = 33,
        SPELLMOD_UNK_35                    = 35, // Bodyguard Visual
    }

    // 5.4.8: upstream SpellWork 5.4.8
    [Flags]
    public enum SpellCastTargetFlags
    {
        TARGET_FLAG_SELF            = 0x00000000,
        TARGET_FLAG_UNUSED_1        = 0x00000001,               // not used
        TARGET_FLAG_UNIT            = 0x00000002,               // pguid
        TARGET_FLAG_UNIT_RAID       = 0x00000004,               // not sent, used to validate target (if raid member)
        TARGET_FLAG_UNIT_PARTY      = 0x00000008,               // not sent, used to validate target (if party member)
        TARGET_FLAG_ITEM            = 0x00000010,               // pguid
        TARGET_FLAG_SOURCE_LOCATION = 0x00000020,               // pguid, 3 float
        TARGET_FLAG_DEST_LOCATION   = 0x00000040,               // pguid, 3 float
        TARGET_FLAG_UNIT_ENEMY      = 0x00000080,               // not sent, used to validate target (if enemy)
        TARGET_FLAG_UNIT_ALLY       = 0x00000100,               // not sent, used to validate target (if ally)
        TARGET_FLAG_CORPSE_ENEMY    = 0x00000200,               // pguid
        TARGET_FLAG_UNIT_DEAD       = 0x00000400,               // not sent, used to validate target (if dead creature)
        TARGET_FLAG_GAMEOBJECT      = 0x00000800,               // pguid, used with TARGET_GAMEOBJECT
        TARGET_FLAG_TRADE_ITEM      = 0x00001000,               // pguid
        TARGET_FLAG_STRING          = 0x00002000,               // string
        TARGET_FLAG_GAMEOBJECT_ITEM = 0x00004000,               // not sent, used with TARGET_GAMEOBJECT_ITEM
        TARGET_FLAG_CORPSE_ALLY     = 0x00008000,               // pguid
        TARGET_FLAG_UNIT_MINIPET    = 0x00010000,               // pguid, used to validate target (if non combat pet)
        TARGET_FLAG_GLYPH_SLOT      = 0x00020000,               // used in glyph spells
        TARGET_FLAG_UNK19           = 0x00040000,               // sometimes appears with DEST_TARGET spells (may appear or not for a given spell)
        TARGET_FLAG_UNUSED20        = 0x00080000,               // uint32 counter, loop { vec3 - screen position (?), guid }, not used so far
        TARGET_FLAG_UNIT_PASSENGER  = 0x00100000,               // guessed, used to validate target (if vehicle passenger)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h Powers
    public enum Powers : sbyte
    {
        POWER_HEALTH          = -2,
        POWER_MANA            = 0,
        POWER_RAGE            = 1,
        POWER_FOCUS           = 2,
        POWER_ENERGY          = 3,
        POWER_LIGHT_FORCE     = 4, // unused since 5.1.0 - WOD NOTE: POWER_COMBO_POINTS
        POWER_RUNES           = 5,
        POWER_RUNIC_POWER     = 6,
        POWER_SOUL_SHARDS     = 7,
        POWER_ECLIPSE         = 8,
        POWER_HOLY_POWER      = 9,
        POWER_ALTERNATE_POWER = 10,
        POWER_DARK_FORCE      = 11,
        POWER_CHI             = 12,
        POWER_SHADOW_ORBS     = 13,
        POWER_BURNING_EMBERS  = 14,
        POWER_DEMONIC_FURY    = 15,
        POWER_ARCANE_CHARGES  = 16,
        MAX_POWERS            = 17,
        POWER_ALL             = 127,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h AuraStateType
    public enum AuraState
    {
        AURA_STATE_NONE                    = 0, // C   |
        AURA_STATE_DEFENSE                 = 1, // C   |
        AURA_STATE_HEALTHLESS_20_PERCENT   = 2, // CcT |
        AURA_STATE_BERSERKING              = 3, // C T |
        AURA_STATE_FROZEN                  = 4, // c t| frozen target
        AURA_STATE_JUDGEMENT               = 5, // C   |
        AURA_STATE_HUNTER_PARRY            = 7, // C   |
        AURA_STATE_WARRIOR_VICTORY_RUSH    = 10, // C   | warrior victory rush
        AURA_STATE_FAERIE_FIRE             = 12, // c t|
        AURA_STATE_HEALTHLESS_35_PERCENT   = 13, // C T |
        AURA_STATE_CONFLAGRATE             = 14, // T |
        AURA_STATE_SWIFTMEND               = 15, // T |
        AURA_STATE_DEADLY_POISON           = 16, // T |
        AURA_STATE_ENRAGE                  = 17, // C   |
        AURA_STATE_BLEEDING                = 18, // T|
        AURA_STATE_UNKNOWN19               = 19, // |
        AURA_STATE_UNKNOWN22               = 22, // C  t| varius spells (63884, 50240)
        AURA_STATE_HEALTH_ABOVE_75_PERCENT = 23, // C   |
    }

    // 5.4.8: upstream SpellWork 5.4.8
    [Flags]
    enum InventoryTypeMask
    {
        ALL             = -1,
        NON_EQUIP       = 1 << 0,
        HEAD            = 1 << 1,
        NECK            = 1 << 2,
        SHOULDERS       = 1 << 3,
        BODY            = 1 << 4,
        CHEST           = 1 << 5,
        WAIST           = 1 << 6,
        LEGS            = 1 << 7,
        FEET            = 1 << 8,
        WRISTS          = 1 << 9,
        HANDS           = 1 << 10,
        FINGER          = 1 << 11,
        TRINKET         = 1 << 12,
        WEAPON          = 1 << 13,
        SHIELD          = 1 << 14,
        RANGED          = 1 << 15,
        CLOAK           = 1 << 16,
        WEAPON_2H       = 1 << 17,
        BAG             = 1 << 18,
        TABARD          = 1 << 19,
        ROBE            = 1 << 20,
        WEAPONMAINHAND  = 1 << 21,
        WEAPONOFFHAND   = 1 << 22,
        HOLDABLE        = 1 << 23,
        AMMO            = 1 << 24,
        THROWN          = 1 << 25,
        RANGEDRIGHT     = 1 << 26,
        QUIVER          = 1 << 27,
        RELIC           = 1 << 28,
    }

    // 5.4.8: upstream SpellWork 5.4.8
    public enum ItemClass
    {
        CONSUMABLE  = 0,
        CONTAINER   = 1,
        WEAPON      = 2,
        GEM         = 3,
        ARMOR       = 4,
        REAGENT     = 5,
        PROJECTILE  = 6,
        TRADE_GOODS = 7,
        GENERIC     = 8,
        RECIPE      = 9,
        MONEY       = 10,
        QUIVER      = 11,
        QUEST       = 12,
        KEY         = 13,
        PERMANENT   = 14,
        MISC        = 15,
        GLYPH       = 16,
        MAX
    }

    // 5.4.8: upstream SpellWork 5.4.8
    [Flags]
    public enum ItemSubClassWeaponMask
    {
        ALL             = -1,
        AXE             = 1 << 0,
        AXE2            = 1 << 1,
        BOW             = 1 << 2,
        GUN             = 1 << 3,
        MACE            = 1 << 4,
        MACE2           = 1 << 5,
        POLEARM         = 1 << 6,
        SWORD           = 1 << 7,
        SWORD2          = 1 << 8,
        OBSOLETE        = 1 << 9,
        STAFF           = 1 << 10,
        EXOTIC          = 1 << 11,
        EXOTIC2         = 1 << 12,
        FIST            = 1 << 13,
        MISC            = 1 << 14,
        DAGGER          = 1 << 15,
        THROWN          = 1 << 16,
        SPEAR           = 1 << 17,
        CROSSBOW        = 1 << 18,
        WAND            = 1 << 19,
        FISHING_POLE    = 1 << 20
    }

    // 5.4.8: upstream SpellWork 5.4.8
    [Flags]
    public enum ItemSubClassArmorMask
    {
        ALL     = -1,
        MISC    = 1 << 0,
        CLOTH   = 1 << 1,
        LEATHER = 1 << 2,
        MAIL    = 1 << 3,
        PLATE   = 1 << 4,
        BUCKLER = 1 << 5,
        SHIELD  = 1 << 6,
        LIBRAM  = 1 << 7,
        IDOL    = 1 << 8,
        TOTEM   = 1 << 9,
        SIGIL   = 1 << 10
    }

    // 5.4.8: upstream SpellWork 5.4.8
    [Flags]
    public enum ItemSubClassMiscMask
    {
        ALL     = -1,
        JUNK    = 1 << 0,
        REAGENT = 1 << 1,
        PET     = 1 << 2,
        HOLIDAY = 1 << 3,
        OTHER   = 1 << 4,
        MOUNT   = 1 << 5,
    }

    // 5.4.8: upstream SpellWork 5.4.8, plus the two creature types SkyFire_548 adds (WILD_PET, ABERRATION)
    [Flags]
    public enum CreatureTypeMask
    {
        ALL             = -1,
        NONE            = 0,
        BEAST           = 1 << 0,
        DRAGONKIN       = 1 << 1,
        DEMON           = 1 << 2,
        ELEMENTAL       = 1 << 3,
        GIANT           = 1 << 4,
        UNDEAD          = 1 << 5,
        HUMANOID        = 1 << 6,
        CRITTER         = 1 << 7,
        MECHANICAL      = 1 << 8,
        NOT_SPECIFIED   = 1 << 9,
        TOTEM           = 1 << 10,
        NON_COMBAT_PET  = 1 << 11,
        GAS_CLOUD       = 1 << 12,
        WILD_PET        = 1 << 13, // SkyFire_548 SharedDefines.h CREATURE_TYPE_WILD_PET (14)
        ABERRATION      = 1 << 14, // SkyFire_548 SharedDefines.h CREATURE_TYPE_ABERRATION (15)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr0; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtribute : uint
    {
        SPELL_ATTR0_UNK0                          = 0x00000001, // 0 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_REQ_AMMO                      = 0x00000002, // 1 - (5.4.8 SPELL_ATTR0_REQ_RANGED_WEAPON)
        SPELL_ATTR0_ON_NEXT_SWING                 = 0x00000004, // 2 - (5.4.8 SPELL_ATTR0_ON_NEXT_SWING_(NO_DAMAGE)
        SPELL_ATTR0_IS_REPLENISHMENT              = 0x00000008, // 3 not set in 3.0.3 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_ABILITY                       = 0x00000010, // 4 client puts 'ability' instead of 'spell' in game strings for these spells
        SPELL_ATTR0_TRADESPELL                    = 0x00000020, // 5 trade spells (recipes), will be added by client to a sublist of profession spell
        SPELL_ATTR0_PASSIVE                       = 0x00000040, // 6 Passive spell
        SPELL_ATTR0_HIDDEN_CLIENTSIDE             = 0x00000080, // 7 Spells with this attribute are not visible in spellbook or aura bar
        SPELL_ATTR0_HIDE_IN_COMBAT_LOG            = 0x00000100, // 8 This attribite controls whether spell appears in combat logs (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_TARGET_MAINHAND_ITEM          = 0x00000200, // 9 Client automatically selects item from mainhand slot as a cast target (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_ON_NEXT_SWING_2               = 0x00000400, // 10 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_UNK11                         = 0x00000800, // 11 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_DAYTIME_ONLY                  = 0x00001000, // 12 only useable at daytime, not set in 2.4.2 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_NIGHT_ONLY                    = 0x00002000, // 13 only useable at night, not set in 2.4.2 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_INDOORS_ONLY                  = 0x00004000, // 14 only useable indoors, not set in 5.4.8
        SPELL_ATTR0_OUTDOORS_ONLY                 = 0x00008000, // 15 Only useable outdoors.
        SPELL_ATTR0_NOT_SHAPESHIFT                = 0x00010000, // 16 Not while shapeshifted
        SPELL_ATTR0_ONLY_STEALTHED                = 0x00020000, // 17 Must be in stealth
        SPELL_ATTR0_DONT_AFFECT_SHEATH_STATE      = 0x00040000, // 18 client won't hide unit weapons in sheath on cast/channel (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_LEVEL_DAMAGE_CALCULATION      = 0x00080000, // 19 spelldamage depends on caster level
        SPELL_ATTR0_STOP_ATTACK_TARGET            = 0x00100000, // 20 - (5.4.8 SPELL_ATTR0_CANCEL_AUTO_ATTACK) - Stop attack after use this spell (and not begin attack if use)
        SPELL_ATTR0_IMPOSSIBLE_DODGE_PARRY_BLOCK  = 0x00200000, // 21 - (5.4.8 SPELL_ATTR0_NO_ACTIVE_DEFENSE) - Cannot be dodged/parried/blocked
        SPELL_ATTR0_CAST_TRACK_TARGET             = 0x00400000, // 22 Client automatically forces player to face target when casting (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_CASTABLE_WHILE_DEAD           = 0x00800000, // 23 castable while dead?
        SPELL_ATTR0_CASTABLE_WHILE_MOUNTED        = 0x01000000, // 24 castable while mounted
        SPELL_ATTR0_DISABLED_WHILE_ACTIVE         = 0x02000000, // 25 - (5.4.8 SPELL_ATTR0_COOLDOWN_ON_EVENT) - Activate and start cooldown after aura fade or remove summoned creature or go
        SPELL_ATTR0_NEGATIVE_1                    = 0x04000000, // 26 - (5.4.8 SPELL_ATTR0_AURA_IS_DEBUFF) - Many negative spells have this attr
        SPELL_ATTR0_CASTABLE_WHILE_SITTING        = 0x08000000, // 27 castable while sitting (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_CANT_USED_IN_COMBAT           = 0x10000000, // 28 - (5.4.8 SPELL_ATTR0_NOT_IN_COMBAT) - Cannot be used in combat
        SPELL_ATTR0_UNAFFECTED_BY_INVULNERABILITY = 0x20000000, // 29 - (5.4.8 SPELL_ATTR0_NO_IMMUNITY) - unaffected by invulnerability (hmm possible not...)
        SPELL_ATTR0_HEARTBEAT_RESIST_CHECK        = 0x40000000, // 30 random chance the effect will end TODO: implement core support (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR0_CANT_CANCEL                   = 0x80000000, // 31 - (5.4.8 SPELL_ATTR0_NO_AURA_CANCEL) - positive aura can't be canceled
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr1; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx : uint
    {
        SPELL_ATTR1_DISMISS_PET                 = 0x00000001, // 0 for spells without this flag client doesn't allow to summon pet if caster has a pet (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_DRAIN_ALL_POWER             = 0x00000002, // 1 - 5.4.8 use all power (Only paladin Lay of Hands and Bunyanize)
        SPELL_ATTR1_CHANNELED_1                 = 0x00000004, // 2 - (5.4.8 - SPELL_ATTR1_IS_CHANNELED) - clientside checked? cancelable?
        SPELL_ATTR1_CANT_BE_REDIRECTED          = 0x00000008, // 3 - (5.4.8 - SPELL_ATTR1_NO_REDIRECT)
        SPELL_ATTR1_UNK4                        = 0x00000010, // 4 stealth and whirlwind (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_NOT_BREAK_STEALTH           = 0x00000020, // 5 - (5.4.8 - SPELL_ATTR1_ALLOW_WHILE_STEALTH) - Not break stealth
        SPELL_ATTR1_CHANNELED_2                 = 0x00000040, // 6 - (5.4.8 SPELL_ATTR1_IS_SELF_CHANNELED)
        SPELL_ATTR1_CANT_BE_REFLECTED           = 0x00000080, // 7 - (5.4.8 SPELL_ATTR1_NO_REFLECT)
        SPELL_ATTR1_CANT_TARGET_IN_COMBAT       = 0x00000100, // 8 - (5.4.8 SPELL_ATTR1_ONLY_PEACEFULL_TARGET) - can target only out of combat units
        SPELL_ATTR1_MELEE_COMBAT_START          = 0x00000200, // 9 player starts melee combat after this spell is cast (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_NO_THREAT                   = 0x00000400, // 10 no generates threat on cast 100% (old NO_INITIAL_AGGRO)
        SPELL_ATTR1_UNK11                       = 0x00000800, // 11 aura (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_IS_PICKPOCKET               = 0x00001000, // 12 Pickpocket (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_FARSIGHT                    = 0x00002000, // 13 Client removes farsight on aura loss (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_CHANNEL_TRACK_TARGET        = 0x00004000, // 14 Client automatically forces player to face target when channeling (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_DISPEL_AURAS_ON_IMMUNITY    = 0x00008000, // 15 - (5.4.8 SPELL_ATTR1_IMMUNITY_PURGES_EFFECT) - remove auras on immunity
        SPELL_ATTR1_UNAFFECTED_BY_SCHOOL_IMMUNE = 0x00010000, // 16 - (5.4.8 SPELL_ATTR1_IMMUNITY_TO_HOSTILE_AND_FRIENDLY_EFFECTS) on immuniy
        SPELL_ATTR1_UNAUTOCASTABLE_BY_PET       = 0x00020000, // 17 - (5.4.8 SPELL_ATTR1_NO_AUTOCAST_AI)
        SPELL_ATTR1_UNK18                       = 0x00040000, // 18 stun, polymorph, daze, hex (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_CANT_TARGET_SELF            = 0x00080000, // 19 - (5.4.8 SPELL_ATTR1_EXCLUDE_CASTER)
        SPELL_ATTR1_REQ_COMBO_POINTS1           = 0x00100000, // 20 - (5.4.8 SPELL_ATTR1_FINISHING_MOVE_DAMAGE) - Req combo points on target
        SPELL_ATTR1_UNK21                       = 0x00200000, // 21 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_REQ_COMBO_POINTS2           = 0x00400000, // 22 - (5.4.8 SPELL_ATTR1_FINISHING_MOVE_DURATION) - Req combo points on target
        SPELL_ATTR1_UNK23                       = 0x00800000, // 23 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_IS_FISHING                  = 0x01000000, // 24 only fishing spells (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_UNK25                       = 0x02000000, // 25 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_UNK26                       = 0x04000000, // 26 works correctly with [target=focus] and [target=mouseover] macros? (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_UNK27                       = 0x08000000, // 27 melee spell? (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_DONT_DISPLAY_IN_AURA_BAR    = 0x10000000, // 28 client doesn't display these spells in aura bar (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_CHANNEL_DISPLAY_SPELL_NAME  = 0x20000000, // 29 spell name is displayed in cast bar instead of 'channeling' text (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_ENABLE_AT_DODGE             = 0x40000000, // 30 Overpower (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR1_UNK31                       = 0x80000000, // 31 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr2; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx2 : uint
    {
        SPELL_ATTR2_CAN_TARGET_DEAD            = 0x00000001, // 0 - (5.4.8 SPELL_ATTR2_ALLOW_DEAD_TARGET) - can target dead unit or corpse
        SPELL_ATTR2_UNK1                       = 0x00000002, // 1 vanish, shadowform, Ghost Wolf and other (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_CAN_TARGET_NOT_IN_LOS      = 0x00000004, // 2 - (5.4.8 SPELL_ATTR2_IGNORE_LOS)
        SPELL_ATTR2_UNK3                       = 0x00000008, // 3 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_DISPLAY_IN_STANCE_BAR      = 0x00000010, // 4 client displays icon in stance bar when learned, even if not shapeshift (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_AUTOREPEAT_FLAG            = 0x00000020, // 5 - (5.4.8 SPELL_ATTR2_AUTOREPEAT)
        SPELL_ATTR2_CANT_TARGET_TAPPED         = 0x00000040, // 6 - (5.4.8 SPELL_ATTR2_CANT_CAST_ON_TAPPED) - target must be tapped by caster
        SPELL_ATTR2_UNK7                       = 0x00000080, // 7 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK8                       = 0x00000100, // 8 not set in 3.0.3 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK9                       = 0x00000200, // 9 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK10                      = 0x00000400, // 10 related to tame (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_HEALTH_FUNNEL              = 0x00000800, // 11 - (5.4.8 SPELL_ATTR2_NO_TARGET_PER_SEC_COST)
        SPELL_ATTR2_UNK12                      = 0x00001000, // 12 Cleave, Heart Strike, Maul, Sunder Armor, Swipe (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_PRESERVE_ENCHANT_IN_ARENA  = 0x00002000, // 13 - (5.4.8 SPELL_ATTR2_ENCHANT_OWN_ITEM_ONLY) - Items enchanted by spells with this flag preserve the enchant to arenas
        SPELL_ATTR2_UNK14                      = 0x00004000, // 14 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK15                      = 0x00008000, // 15 not set in 3.0.3 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_TAME_BEAST                 = 0x00010000, // 16 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_NOT_RESET_AUTO_ACTIONS     = 0x00020000, // 17 - (5.4.8 SPELL_ATTR2_NO_RESET_COMBAT_TIMERS) - don't reset timers for melee autoattacks (swings) or ranged autoattacks (autoshoots)
        SPELL_ATTR2_REQ_DEAD_PET               = 0x00040000, // 18 Only Revive pet and Heart of the Pheonix (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_NOT_NEED_SHAPESHIFT        = 0x00080000, // 19 does not necessarly need shapeshift
        SPELL_ATTR2_UNK20                      = 0x00100000, // 20 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_DAMAGE_REDUCED_SHIELD      = 0x00200000, // 21 - (5.4.8 SPELL_ATTR2_FAIL_ON_ALL_TARGETS_IMMUNE) - for ice blocks, pala immunity buffs, priest absorb shields, but used also for other spells -> not sure!
        SPELL_ATTR2_UNK22                      = 0x00400000, // 22 Ambush, Backstab, Cheap Shot, Death Grip, Garrote, Judgements, Mutilate, Pounce, Ravage, Shiv, Shred (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_IS_ARCANE_CONCENTRATION    = 0x00800000, // 23 Only mage Arcane Concentration have this flag (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK24                      = 0x01000000, // 24 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK25                      = 0x02000000, // 25 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK26                      = 0x04000000, // 26 unaffected by school immunity (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK27                      = 0x08000000, // 27 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_UNK28                      = 0x10000000, // 28 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR2_CANT_CRIT                  = 0x20000000, // 29 Spell can't crit
        SPELL_ATTR2_TRIGGERED_CAN_TRIGGER_PROC = 0x40000000, // 30 - (5.4.8 SPELL_ATTR2_ACTIVE_THREAT) - spell can trigger even if triggered
        SPELL_ATTR2_FOOD_BUFF                  = 0x80000000, // 31 Food or Drink Buff (like Well Fed) (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr3; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx3 : uint
    {
        SPELL_ATTR3_UNK0                         = 0x00000001, // 0 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_UNK1                         = 0x00000002, // 1 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_UNK2                         = 0x00000004, // 2 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_BLOCKABLE_SPELL              = 0x00000008, // 3 - (5.4.8 SPELL_ATTR3_COMPLETE_BLOCK) - Only dmg class melee in 3.1.3
        SPELL_ATTR3_IGNORE_RESURRECTION_TIMER    = 0x00000010, // 4 you don't have to wait to be resurrected with these spells (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_UNK5                         = 0x00000020, // 5 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_UNK6                         = 0x00000040, // 6 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_STACK_FOR_DIFF_CASTERS       = 0x00000080, // 7 - (5.4.8 SPELL_ATTR3_DOT_STACKING_RULE) - separate stack for every caster
        SPELL_ATTR3_ONLY_TARGET_PLAYERS          = 0x00000100, // 8 - (5.4.8 SPELL_ATTR3_ONLY_ON_PLAYER) - can only target players
        SPELL_ATTR3_TRIGGERED_CAN_TRIGGER_PROC_2 = 0x00000200, // 9 - (5.4.8 SPELL_ATTR3_NOT_A_PROC) - triggered from effect?
        SPELL_ATTR3_MAIN_HAND                    = 0x00000400, // 10 - (5.4.8 SPELL_ATTR3_REQ_MAIN_HAND) - Main hand weapon required
        SPELL_ATTR3_BATTLEGROUND                 = 0x00000800, // 11 - (5.4.8 SPELL_ATTR3_ONLY_BG) - Can casted only on battleground
        SPELL_ATTR3_ONLY_TARGET_GHOSTS           = 0x00001000, // 12 - (5.4.8 SPELL_ATTR3_ONLY_GHOST)
        SPELL_ATTR3_DONT_DISPLAY_CHANNEL_BAR     = 0x00002000, // 13 Clientside attribute - will not display channeling bar (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_IS_HONORLESS_TARGET          = 0x00004000, // 14 "Honorless Target" only this spells have this flag (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_UNK15                        = 0x00008000, // 15 Auto Shoot, Shoot, Throw,  - this is autoshot flag (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_CANT_TRIGGER_PROC            = 0x00010000, // 16 - (5.4.8 SPELL_ATTR3_SUPRESS_CASTER_PROC) - confirmed with many patchnotes
        SPELL_ATTR3_NO_INITIAL_AGGRO             = 0x00020000, // 17 - (5.4.8 SPELL_ATTR3_SUPPRES_TARGET_PROC) - Soothe Animal, 39758, Mind Soothe
        SPELL_ATTR3_IGNORE_HIT_RESULT            = 0x00040000, // 18 - (5.4.8 SPELL_ATTR3_ALLWAYS_HIT) - Spell should always hit its target
        SPELL_ATTR3_DISABLE_PROC                 = 0x00080000, // 19 - (5.4.8 SPELL_ATTR3_INSTANT_TARGET_PROC) - during aura proc no spells can trigger (20178, 20375)
        SPELL_ATTR3_DEATH_PERSISTENT             = 0x00100000, // 20 - (5.4.8 SPELL_ATTR3_ALLOW_AURA_WHILE_DEAD) - Death persistent spells
        SPELL_ATTR3_UNK21                        = 0x00200000, // 21 unused (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_REQ_WAND                     = 0x00400000, // 22 Req wand (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_UNK23                        = 0x00800000, // 23 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_REQ_OFFHAND                  = 0x01000000, // 24 Req offhand weapon
        SPELL_ATTR3_UNK25                        = 0x02000000, // 25 no cause spell pushback ? (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_CAN_PROC_WITH_TRIGGERED      = 0x04000000, // 26 - (5.4.8 SPELL_ATTR3_CAN_PROC_FROM_PROC) - auras with this attribute can proc from triggered spell casts with SPELL_ATTR3_TRIGGERED_CAN_TRIGGER_PROC_2 (67736 + 52999)
        SPELL_ATTR3_DRAIN_SOUL                   = 0x08000000, // 27 only drain soul has this flag (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_UNK28                        = 0x10000000, // 28 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_NO_DONE_BONUS                = 0x20000000, // 29 - (5.4.8 SPELL_ATTR3_IGNORE_CASTER_MODIFIERS) - Ignore caster spellpower and done damage mods?  client doesn't apply spellmods for those spells
        SPELL_ATTR3_DONT_DISPLAY_RANGE           = 0x40000000, // 30 client doesn't display range in tooltip for those spells (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR3_UNK31                        = 0x80000000, // 31 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr4; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx4 : uint
    {
        SPELL_ATTR4_IGNORE_RESISTANCES              = 0x00000001, // 0 - (5.4.8 SPELL_ATTR4_NO_CAST_LOG) - spells with this attribute will completely ignore the target's resistance (these spells can't be resisted)
        SPELL_ATTR4_PROC_ONLY_ON_CASTER             = 0x00000002, // 1 - (5.4.8 SPELL_ATTR4_CLASS_TRIGGER_ONLY_ON_TARGET) - proc only on effects with TARGET_UNIT_CASTER?
        SPELL_ATTR4_UNK2                            = 0x00000004, // 2 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK3                            = 0x00000008, // 3 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK4                            = 0x00000010, // 4 - (5.4.8 SPELL_ATTR4_NO_HARMFUL_THREAT) - This will no longer cause guards to attack on use??
        SPELL_ATTR4_UNK5                            = 0x00000020, // 5 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_NOT_STEALABLE                   = 0x00000040, // 6 although such auras might be dispellable, they cannot be stolen
        SPELL_ATTR4_TRIGGERED                       = 0x00000080, // 7 - (5.4.8 SPELL_ATTR4_ALLOW_CAST_WHILE_CASTING) - spells forced to be triggered
        SPELL_ATTR4_FIXED_DAMAGE                    = 0x00000100, // 8 ignores taken percent damage mods? (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_TRIGGER_ACTIVATE                = 0x00000200, // 9 initially disabled / trigger activate from event (Execute, Riposte, Deep Freeze end other) (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_SPELL_VS_EXTEND_COST            = 0x00000400, // 10 Rogue Shiv have this flag (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK11                           = 0x00000800, // 11 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK12                           = 0x00001000, // 12 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK13                           = 0x00002000, // 13 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_DAMAGE_DOESNT_BREAK_AURAS       = 0x00004000, // 14 - (5.4.8 SPELL_ATTR4_REACTIVE_DMG_PROC) - doesn't break auras by damage from these spells
        SPELL_ATTR4_UNK15                           = 0x00008000, // 15 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_NOT_USABLE_IN_ARENA_OR_RATED_BG = 0x00010000, // 16 Cannot be used in both Arenas or Rated Battlegrounds
        SPELL_ATTR4_USABLE_IN_ARENA                 = 0x00020000, // 17 - (5.4.8 SPELL_ATTR4_IGNORE_DEFAULT_ARENA_RESTRICTION)
        SPELL_ATTR4_AREA_TARGET_CHAIN               = 0x00040000, // 18 - (5.4.8 SPELL_ATTR4_BOUNCY_CHAIN_MISSILES)(NYI)hits area targets one after another instead of all at once
        SPELL_ATTR4_UNK19                           = 0x00080000, // 19 proc dalayed, after damage or don't proc on absorb? (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_NOT_CHECK_SELFCAST_POWER        = 0x00100000, // 20 supersedes message "More powerful spell applied" for self casts. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK21                           = 0x00200000, // 21 - (5.4.8 SPELL_ATTR4_ALLOW_ENTERING_ARENA) - Pally aura, dk presence, dudu form, warrior stance, shadowform, hunter track
        SPELL_ATTR4_UNK22                           = 0x00400000, // 22 Seal of Command (42058, 57770) and Gymer's Smash 55426 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK23                           = 0x00800000, // 23 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK24                           = 0x01000000, // 24 some shoot spell (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_IS_PET_SCALING                  = 0x02000000, // 25 pet scaling auras (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_CAST_ONLY_IN_OUTLAND            = 0x04000000, // 26 - (5.4.8 SPELL_ATTR4_ONLY_FLYING_AREAS) Can only be used in Outland.
        SPELL_ATTR4_UNK27                           = 0x08000000, // 27 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK28                           = 0x10000000, // 28 Aimed Shot (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK29                           = 0x20000000, // 29 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK30                           = 0x40000000, // 30 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR4_UNK31                           = 0x80000000, // 31 Polymorph (chicken) 228 and Sonic Boom (38052, 38488) (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr5; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx5 : uint
    {
        SPELL_ATTR5_UNK0                             = 0x00000001, // 0 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_NO_REAGENT_WHILE_PREP            = 0x00000002, // 1 - (5.4.8 SPELL_ATTR5_NO_REAGENT_COST_WITH_AURA) not need reagents if UNIT_FLAG_PREPARATION
        SPELL_ATTR5_UNK2                             = 0x00000004, // 2 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_USABLE_WHILE_STUNNED             = 0x00000008, // 3 - (5.4.8 SPELL_ATTR5_ALLOW_WHILE_STUNNED) usable while stunned
        SPELL_ATTR5_UNK4                             = 0x00000010, // 4 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_SINGLE_TARGET_SPELL              = 0x00000020, // 5 - (5.4.8 SPELL_ATTR5_LIMIT_N) Only one target can be apply at a time
        SPELL_ATTR5_UNK6                             = 0x00000040, // 6 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK7                             = 0x00000080, // 7 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK8                             = 0x00000100, // 8 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_START_PERIODIC_AT_APPLY          = 0x00000200, // 9 - (5.4.8 SPELL_ATTR5_EXTRA_INIT_PERIOD) begin periodic tick at aura apply
        SPELL_ATTR5_HIDE_DURATION                    = 0x00000400, // 10 - (5.4.8 SPELL_ATTR5_NO_DURATION_DISPLAY) do not send duration to client
        SPELL_ATTR5_ALLOW_TARGET_OF_TARGET_AS_TARGET = 0x00000800, // 11 (NYI) uses target's target as target if original target not valid (intervene for example) (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK12                            = 0x00001000, // 12 Cleave related? (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_HASTE_AFFECT_DURATION            = 0x00002000, // 13 - (5.4.8 SPELL_ATTR5_SPELLHASTE_AFFECTS_PERIODIC) haste effects decrease duration of this
        SPELL_ATTR5_UNK14                            = 0x00004000, // 14 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK15                            = 0x00008000, // 15 Inflits on multiple targets? (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_SPECIAL_ITEM_CLASS_CHECK         = 0x00010000, // 16 this allows spells with EquippedItemClass to affect spells from other items if the required item is equipped
        SPELL_ATTR5_USABLE_WHILE_FEARED              = 0x00020000, // 17 - (5.4.8 SPELL_ATTR5_ALLOW_WHILE_FLEE) usable while feared
        SPELL_ATTR5_USABLE_WHILE_CONFUSED            = 0x00040000, // 18 - (5.4.8 SPELL_ATTR5_ALLOW_WHILE_CONFUSED) usable while confused
        SPELL_ATTR5_DONT_TURN_DURING_CAST            = 0x00080000, // 19 - (5.4.8 SPELL_ATTR5_AI_NO_FACE_TARGET) Blocks caster's turning when casting (client does not automatically turn caster's model to face UNIT_FIELD_TARGET)
        SPELL_ATTR5_UNK20                            = 0x00100000, // 20 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK21                            = 0x00200000, // 21 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK22                            = 0x00400000, // 22 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK23                            = 0x00800000, // 23 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK24                            = 0x01000000, // 24 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK25                            = 0x02000000, // 25 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK26                            = 0x04000000, // 26 aoe related - Boulder, Cannon, Corpse Explosion, Fire Nova, Flames, Frost Bomb, Living Bomb, Seed of Corruption, Starfall, Thunder Clap, Volley (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_DONT_SHOW_AURA_IF_SELF_CAST      = 0x08000000, // 27 Auras with this attribute are not visible on units that are the caster (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_DONT_SHOW_AURA_IF_NOT_SELF_CAST  = 0x10000000, // 28 Auras with this attribute are not visible on units that are not the caster (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK29                            = 0x20000000, // 29 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK30                            = 0x40000000, // 30 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR5_UNK31                            = 0x80000000, // 31 Forces all nearby enemies to focus attacks caster (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr6; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx6 : uint
    {
        SPELL_ATTR6_DONT_DISPLAY_COOLDOWN         = 0x00000001, // 0 client doesn't display cooldown in tooltip for these spells (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_ONLY_IN_ARENA                 = 0x00000002, // 1 only usable in arena (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_IGNORE_CASTER_AURAS           = 0x00000004, // 2 - (5.4.8 SPELL_ATTR6_NOT_AN_ATTACK)
        SPELL_ATTR6_ASSIST_IGNORE_IMMUNE_FLAG     = 0x00000008, // 3 - (5.4.8 SPELL_ATTR6_CAN_ASSIST_IMMUNE_PC) skips checking UNIT_FLAG_IMMUNE_TO_PC and UNIT_FLAG_IMMUNE_TO_NPC flags on assist
        SPELL_ATTR6_UNK4                          = 0x00000010, // 4 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK5                          = 0x00000020, // 5 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_USE_SPELL_CAST_EVENT          = 0x00000040, // 6 Auras with this attribute trigger SPELL_CAST combat log event instead of SPELL_AURA_START (clientside attribute) (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK7                          = 0x00000080, // 7 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_CANT_TARGET_CROWD_CONTROLLED  = 0x00000100, // 8 - (5.4.8 SPELL_ATTR6_NO_CHAIN_CC_TARGET)
        SPELL_ATTR6_UNK9                          = 0x00000200, // 9 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_CAN_TARGET_POSSESSED_FRIENDS  = 0x00000400, // 10 NYI! (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_NOT_IN_RAID_INSTANCE          = 0x00000800, // 11 - (5.4.8 SPELL_ATTR6_NOT_IN_RAID_INSTANCES) not usable in raid instance
        SPELL_ATTR6_CASTABLE_WHILE_ON_VEHICLE     = 0x00001000, // 12 - (5.4.8 SPELL_ATTR6_ALLOW_WHILE_RIDING_VEHICLE) castable while caster is on vehicle
        SPELL_ATTR6_CAN_TARGET_INVISIBLE          = 0x00002000, // 13 - (5.4.8 SPELL_ATTR6_IGNORE_PHASESHIFT) ignore visibility requirement for spell target (phases, invisibility, etc.)
        SPELL_ATTR6_UNK14                         = 0x00004000, // 14 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK15                         = 0x00008000, // 15 only 54368, 67892 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK16                         = 0x00010000, // 16 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK17                         = 0x00020000, // 17 Mount spell (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_CAST_BY_CHARMER               = 0x00040000, // 18 - (5.4.8 SPELL_ATTR6_ORIGIN_FROM_CONTROLER) client won't allow to cast these spells when unit is not possessed && charmer of caster will be original caster
        SPELL_ATTR6_UNK19                         = 0x00080000, // 19 only 47488, 50782 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_ONLY_VISIBLE_TO_CASTER        = 0x00100000, // 20 Auras with this attribute are only visible to their caster (or pet's owner) (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_CLIENT_UI_TARGET_EFFECTS      = 0x00200000, // 21 it's only client-side attribute (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK22                         = 0x00400000, // 22 only 72054 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK23                         = 0x00800000, // 23 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_CAN_TARGET_UNTARGETABLE       = 0x01000000, // 24
        SPELL_ATTR6_UNK25                         = 0x02000000, // 25 Exorcism, Flash of Light (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK26                         = 0x04000000, // 26 related to player castable positive buff (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK27                         = 0x08000000, // 27 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_UNK28                         = 0x10000000, // 28 Death Grip (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_NO_DONE_PCT_DAMAGE_MODS       = 0x20000000, // 29 - (5.4.8 SPELL_ATTR6_IGNORE_CASTER_DAMAGE_MODS) ignores done percent damage mods?
        SPELL_ATTR6_UNK30                         = 0x40000000, // 30 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR6_IGNORE_CATEGORY_COOLDOWN_MODS = 0x80000000, // 31 - (5.4.8 SPELL_ATTR6_NO_CATEGORY_COOLDON_MODS)Spells with this attribute skip applying modifiers to category cooldowns
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr7; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx7 : uint
    {
        SPELL_ATTR7_UNK0                        = 0x00000001, // 0 SkyFire_548 comments this bit out (UNK0 / ALLOW_SPELL_REFLECT); upstream 5.4.8 SpellWork does not name it
        SPELL_ATTR7_UNK1                        = 0x00000002, // 1 Not set in 3.2.2a. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_REACTIVATE_AT_RESURRECT     = 0x00000004, // 2 Paladin's auras and 65607 only. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_IS_CHEAT_SPELL              = 0x00000008, // 3 - (5.4.8 SPELL_ATTR7_DEBUG_SPELL) Cannot cast if caster doesn't have UnitFlag2 & UNIT_FLAG2_ALLOW_CHEAT_SPELLS
        SPELL_ATTR7_UNK4                        = 0x00000010, // 4 Only 47883 (Soulstone Resurrection) and test spell. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_SUMMON_TOTEM                = 0x00000020, // 5 Only Shaman totems. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_NO_PUSHBACK_ON_DAMAGE       = 0x00000040, // 6 Does not cause spell pushback on damage (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK7                        = 0x00000080, // 7 66218 (Launch) spell. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_HORDE_ONLY                  = 0x00000100, // 8 Teleports, mounts and other spells. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_ALLIANCE_ONLY               = 0x00000200, // 9 Teleports, mounts and other spells. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_DISPEL_CHARGES              = 0x00000400, // 10 Dispel and Spellsteal individual charges instead of whole aura.
        SPELL_ATTR7_INTERRUPT_ONLY_NONPLAYER    = 0x00000800, // 11 Only non-player casts interrupt, though Feral Charge - Bear has it. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK12                       = 0x00001000, // 12 Not set in 3.2.2a. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK13                       = 0x00002000, // 13 Not set in 3.2.2a. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK14                       = 0x00004000, // 14 Only 52150 (Raise Dead - Pet) spell. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK15                       = 0x00008000, // 15 Exorcism. Usable on players? 100% crit chance on undead and demons? (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_CAN_RESTORE_SECONDARY_POWER = 0x00010000, // 16 These spells can replenish a powertype, which is not the current powertype (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK17                       = 0x00020000, // 17 Only 27965 (Suicide) spell. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_HAS_CHARGE_EFFECT           = 0x00040000, // 18 Only spells that have Charge among effects. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_ZONE_TELEPORT               = 0x00080000, // 19 Teleports to specific zones. (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK20                       = 0x00100000, // 20 Blink, Divine Shield, Ice Block (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK21                       = 0x00200000, // 21 Not set (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK22                       = 0x00400000, // 22 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK23                       = 0x00800000, // 23 Motivate, Mutilate, Shattering Throw (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK24                       = 0x01000000, // 24 Motivate, Mutilate, Perform Speech, Shattering Throw (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK25                       = 0x02000000, // 25 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK26                       = 0x04000000, // 26 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK27                       = 0x08000000, // 27 Not set (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_CONSOLIDATED_RAID_BUFF      = 0x10000000, // 28 May be collapsed in raid buff frame (clientside attribute) (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK29                       = 0x20000000, // 29 only 69028, 71237 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_UNK30                       = 0x40000000, // 30 Burning Determination, Divine Sacrifice, Earth Shield, Prayer of Mending (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR7_CLIENT_INDICATOR            = 0x80000000, // 31 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr8; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx8 : uint
    {
        SPELL_ATTR8_CANT_MISS                       = 0x00000001, // 0 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK1                            = 0x00000002, // 1 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK2                            = 0x00000004, // 2 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK3                            = 0x00000008, // 3 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK4                            = 0x00000010, // 4 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK5                            = 0x00000020, // 5 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK6                            = 0x00000040, // 6 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK7                            = 0x00000080, // 7 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_AFFECT_PARTY_AND_RAID           = 0x00000100, // 8 Nearly all spells have "all party and raid" in description (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_DONT_RESET_PERIODIC_TIMER       = 0x00000200, // 9 - (5.4.8 SPELL_ATTR8_PERIODIC_CAN_CRIT) Periodic auras with this flag keep old periodic timer when refreshing at close to one tick remaining (kind of anti DoT clipping)
        SPELL_ATTR8_NAME_CHANGED_DURING_TRANSFORM   = 0x00000400, // 10 according to wowhead comments, name changes, title remains (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK11                           = 0x00000800, // 11 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_AURA_SEND_AMOUNT                = 0x00001000, // 12 - (5.4.8 SPELL_ATTR8_AURA_POINTS_ON_CLIENT) Aura must have flag AFLAG_ANY_EFFECT_AMOUNT_SENT to send amount
        SPELL_ATTR8_UNK13                           = 0x00002000, // 13 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK14                           = 0x00004000, // 14 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_WATER_MOUNT                     = 0x00008000, // 15 only one River Boat used in Thousand Needles (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK16                           = 0x00010000, // 16 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK17                           = 0x00020000, // 17 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_REMEMBER_SPELLS                 = 0x00040000, // 18 at some point in time, these auras remember spells and allow to cast them later (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_USE_COMBO_POINTS_ON_ANY_TARGET  = 0x00080000, // 19 allows to consume combo points from dead targets (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_ARMOR_SPECIALIZATION            = 0x00100000, // 20 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK21                           = 0x00200000, // 21 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK22                           = 0x00400000, // 22 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_COMBAT_RESURECTION_LIMIT        = 0x00800000, // 23 - (5.4.8 SPELL_ATTR8_COMBAT_RESURECTION_LIMIT)
        SPELL_ATTR8_HEALING_SPELL                   = 0x01000000, // 24 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK25                           = 0x02000000, // 25 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_RAID_MARKER                     = 0x04000000, // 26 probably spell no need learn to cast (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_UNK27                           = 0x08000000, // 27 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_NOT_IN_BG_OR_ARENA              = 0x10000000, // 28 not allow to cast or deactivate currently active effect, not sure about Fast Track (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_MASTERY_SPECIALIZATION          = 0x20000000, // 29 - (5.4.8 SPELL_ATTR8_MASTERY_AFFECTS_POINTS)
        SPELL_ATTR8_UNK30                           = 0x40000000, // 30 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR8_ATTACK_IGNORE_IMMUNE_TO_PC_FLAG = 0x80000000, // 31 - (5.4.8 SPELL_ATTR8_CAN_ATTACK_IMMUNE_PC) Do not check UNIT_FLAG_IMMUNE_TO_PC in IsValidAttackTarget
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr9; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx9 : uint
    {
        SPELL_ATTR9_UNK0                          = 0x00000001, // 0 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK1                          = 0x00000002, // 1 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_RESTRICTED_FLIGHT_AREA        = 0x00000004, // 2 Dalaran and Wintergrasp flight area auras have it (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK3                          = 0x00000008, // 3 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_SPECIAL_DELAY_CALCULATION     = 0x00000010, // 4 - (5.4.8 SPELL_ATTR9_MISSILE_SPEED_IS_DELAY)
        SPELL_ATTR9_SUMMON_PLAYER_TOTEM           = 0x00000020, // 5 - (5.4.8 SPELL_ATTR9_IGNORE_TOTEM_REQUIREMENT)
        SPELL_ATTR9_UNK6                          = 0x00000040, // 6 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK7                          = 0x00000080, // 7 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_AIMED_SHOT                    = 0x00000100, // 8 - (5.4.8 SPELL_ATTR9_COOLDOWN_IGNORE_RANGE_WEAPON)
        SPELL_ATTR9_NOT_USABLE_IN_ARENA           = 0x00000200, // 9 Cannot be used in arenas
        SPELL_ATTR9_UNK10                         = 0x00000400, // 10 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK11                         = 0x00000800, // 11 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK12                         = 0x00001000, // 12 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_SLAM                          = 0x00002000, // 13 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_USABLE_IN_RATED_BATTLEGROUNDS = 0x00004000, // 14 - (5.4.8 SPELL_ATTR9_IGNORE_RATED_BG_RESTRICTIONS) Can be used in Rated Battlegrounds
        SPELL_ATTR9_UNK15                         = 0x00008000, // 15 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK16                         = 0x00010000, // 16 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_BY_DEFAULT_AUTOCAST_OFF       = 0x00020000, // 17 Pet spell defaults to autocast off
        SPELL_ATTR9_UNK18                         = 0x00040000, // 18 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK19                         = 0x00080000, // 19 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK20                         = 0x00100000, // 20 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK21                         = 0x00200000, // 21 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK22                         = 0x00400000, // 22 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK23                         = 0x00800000, // 23 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK24                         = 0x01000000, // 24 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK25                         = 0x02000000, // 25 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK26                         = 0x04000000, // 26 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK27                         = 0x08000000, // 27 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK28                         = 0x10000000, // 28 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK29                         = 0x20000000, // 29 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK30                         = 0x40000000, // 30 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
        SPELL_ATTR9_UNK31                         = 0x80000000, // 31 (upstream 5.4.8 SpellWork; not defined in SkyFire_548)
    }

    // 5.4.8: SkyFire_548 SharedDefines.h SpellAttr10; bits it leaves undefined from upstream SpellWork 5.4.8
    [Flags]
    public enum SpellAtributeEx10 : uint
    {
        SPELL_ATTR10_UNK0                  = 0x00000001, // 0
        SPELL_ATTR10_UNK1                  = 0x00000002, // 1
        SPELL_ATTR10_UNK2                  = 0x00000004, // 2
        SPELL_ATTR10_UNK3                  = 0x00000008, // 3
        SPELL_ATTR10_WATER_SPOUT           = 0x00000010, // 4
        SPELL_ATTR10_UNK5                  = 0x00000020, // 5
        SPELL_ATTR10_UNK6                  = 0x00000040, // 6
        SPELL_ATTR10_TELEPORT_PLAYER       = 0x00000080, // 7 4 Teleport Player spells
        SPELL_ATTR10_UNK8                  = 0x00000100, // 8
        SPELL_ATTR10_UNK9                  = 0x00000200, // 9
        SPELL_ATTR10_UNK10                 = 0x00000400, // 10
        SPELL_ATTR10_HERB_GATHERING_MINING = 0x00000800, // 11 Only Herb Gathering and Mining
        SPELL_ATTR10_UNK12                 = 0x00001000, // 12
        SPELL_ATTR10_UNK13                 = 0x00002000, // 13
        SPELL_ATTR10_UNK14                 = 0x00004000, // 14
        SPELL_ATTR10_UNK15                 = 0x00008000, // 15
        SPELL_ATTR10_UNK16                 = 0x00010000, // 16
        SPELL_ATTR10_UNK17                 = 0x00020000, // 17
        SPELL_ATTR10_UNK18                 = 0x00040000, // 18
        SPELL_ATTR10_UNK19                 = 0x00080000, // 19
        SPELL_ATTR10_UNK20                 = 0x00100000, // 20
        SPELL_ATTR10_UNK21                 = 0x00200000, // 21
        SPELL_ATTR10_UNK22                 = 0x00400000, // 22
        SPELL_ATTR10_UNK23                 = 0x00800000, // 23
        SPELL_ATTR10_UNK24                 = 0x01000000, // 24
        SPELL_ATTR10_UNK25                 = 0x02000000, // 25
        SPELL_ATTR10_UNK26                 = 0x04000000, // 26
        SPELL_ATTR10_UNK27                 = 0x08000000, // 27
        SPELL_ATTR10_UNK28                 = 0x10000000, // 28
        SPELL_ATTR10_UNK29                 = 0x20000000, // 29
        SPELL_ATTR10_UNK30                 = 0x40000000, // 30
        SPELL_ATTR10_UNK31                 = 0x80000000, // 31
    }

    // 5.4.8: upstream SpellWork 5.4.8 (SkyFire_548 SharedDefines.h stops at SpellAttr10)
    [Flags]
    public enum SpellAtributeEx11 : uint
    {
        SPELL_ATTR11_UNK0 = 0x00000001, //  0
        SPELL_ATTR11_UNK1 = 0x00000002, //  1
        SPELL_ATTR11_UNK2 = 0x00000004, //  2
        SPELL_ATTR11_UNK3 = 0x00000008, //  3
        SPELL_ATTR11_UNK4 = 0x00000010, //  4
        SPELL_ATTR11_UNK5 = 0x00000020, //  5
        SPELL_ATTR11_UNK6 = 0x00000040, //  6
        SPELL_ATTR11_UNK7 = 0x00000080, //  7
        SPELL_ATTR11_UNK8 = 0x00000100, //  8
        SPELL_ATTR11_UNK9 = 0x00000200, //  9
        SPELL_ATTR11_UNK10 = 0x00000400, // 10
        SPELL_ATTR11_UNK11 = 0x00000800, // 11
        SPELL_ATTR11_UNK12 = 0x00001000, // 12
        SPELL_ATTR11_UNK13 = 0x00002000, // 13
        SPELL_ATTR11_UNK14 = 0x00004000, // 14
        SPELL_ATTR11_UNK15 = 0x00008000, // 15
        SPELL_ATTR11_UNK16 = 0x00010000, // 16
        SPELL_ATTR11_UNK17 = 0x00020000, // 17
        SPELL_ATTR11_UNK18 = 0x00040000, // 18
        SPELL_ATTR11_UNK19 = 0x00080000, // 19
        SPELL_ATTR11_UNK20 = 0x00100000, // 20
        SPELL_ATTR11_UNK21 = 0x00200000, // 21
        SPELL_ATTR11_UNK22 = 0x00400000, // 22
        SPELL_ATTR11_UNK23 = 0x00800000, // 23
        SPELL_ATTR11_UNK24 = 0x01000000, // 24
        SPELL_ATTR11_UNK25 = 0x02000000, // 25
        SPELL_ATTR11_UNK26 = 0x04000000, // 26
        SPELL_ATTR11_UNK27 = 0x08000000, // 27
        SPELL_ATTR11_UNK28 = 0x10000000, // 28
        SPELL_ATTR11_UNK29 = 0x20000000, // 29
        SPELL_ATTR11_UNK30 = 0x40000000, // 30
        SPELL_ATTR11_UNK31 = 0x80000000  // 31
    }

    // 5.4.8: upstream SpellWork 5.4.8 (SkyFire_548 SharedDefines.h stops at SpellAttr10)
    [Flags]
    public enum SpellAtributeEx12 : uint
    {
        SPELL_ATTR12_UNK0 = 0x00000001, //  0
        SPELL_ATTR12_UNK1 = 0x00000002, //  1
        SPELL_ATTR12_UNK2 = 0x00000004, //  2
        SPELL_ATTR12_UNK3 = 0x00000008, //  3
        SPELL_ATTR12_UNK4 = 0x00000010, //  4
        SPELL_ATTR12_UNK5 = 0x00000020, //  5
        SPELL_ATTR12_UNK6 = 0x00000040, //  6
        SPELL_ATTR12_UNK7 = 0x00000080, //  7
        SPELL_ATTR12_UNK8 = 0x00000100, //  8
        SPELL_ATTR12_UNK9 = 0x00000200, //  9
        SPELL_ATTR12_UNK10 = 0x00000400, // 10
        SPELL_ATTR12_UNK11 = 0x00000800, // 11
        SPELL_ATTR12_UNK12 = 0x00001000, // 12
        SPELL_ATTR12_UNK13 = 0x00002000, // 13
        SPELL_ATTR12_UNK14 = 0x00004000, // 14
        SPELL_ATTR12_UNK15 = 0x00008000, // 15
        SPELL_ATTR12_UNK16 = 0x00010000, // 16
        SPELL_ATTR12_UNK17 = 0x00020000, // 17
        SPELL_ATTR12_UNK18 = 0x00040000, // 18
        SPELL_ATTR12_UNK19 = 0x00080000, // 19
        SPELL_ATTR12_UNK20 = 0x00100000, // 20
        SPELL_ATTR12_UNK21 = 0x00200000, // 21
        SPELL_ATTR12_UNK22 = 0x00400000, // 22
        SPELL_ATTR12_UNK23 = 0x00800000, // 23
        SPELL_ATTR12_UNK24 = 0x01000000, // 24
        SPELL_ATTR12_UNK25 = 0x02000000, // 25
        SPELL_ATTR12_UNK26 = 0x04000000, // 26
        SPELL_ATTR12_UNK27 = 0x08000000, // 27
        SPELL_ATTR12_UNK28 = 0x10000000, // 28
        SPELL_ATTR12_UNK29 = 0x20000000, // 29
        SPELL_ATTR12_UNK30 = 0x40000000, // 30
        SPELL_ATTR12_UNK31 = 0x80000000  // 31
    }

    // 5.4.8: upstream SpellWork 5.4.8 (SkyFire_548 SharedDefines.h stops at SpellAttr10)
    [Flags]
    public enum SpellAtributeEx13 : uint
    {
        SPELL_ATTR13_UNK0 = 0x00000001, //  0
        SPELL_ATTR13_UNK1 = 0x00000002, //  1
        SPELL_ATTR13_UNK2 = 0x00000004, //  2
        SPELL_ATTR13_UNK3 = 0x00000008, //  3
        SPELL_ATTR13_UNK4 = 0x00000010, //  4
        SPELL_ATTR13_UNK5 = 0x00000020, //  5
        SPELL_ATTR13_UNK6 = 0x00000040, //  6
        SPELL_ATTR13_UNK7 = 0x00000080, //  7
        SPELL_ATTR13_UNK8 = 0x00000100, //  8
        SPELL_ATTR13_UNK9 = 0x00000200, //  9
        SPELL_ATTR13_UNK10 = 0x00000400, // 10
        SPELL_ATTR13_UNK11 = 0x00000800, // 11
        SPELL_ATTR13_UNK12 = 0x00001000, // 12
        SPELL_ATTR13_UNK13 = 0x00002000, // 13
        SPELL_ATTR13_UNK14 = 0x00004000, // 14
        SPELL_ATTR13_UNK15 = 0x00008000, // 15
        SPELL_ATTR13_UNK16 = 0x00010000, // 16
        SPELL_ATTR13_UNK17 = 0x00020000, // 17
        SPELL_ATTR13_UNK18 = 0x00040000, // 18
        SPELL_ATTR13_UNK19 = 0x00080000, // 19
        SPELL_ATTR13_UNK20 = 0x00100000, // 20
        SPELL_ATTR13_UNK21 = 0x00200000, // 21
        SPELL_ATTR13_UNK22 = 0x00400000, // 22
        SPELL_ATTR13_UNK23 = 0x00800000, // 23
        SPELL_ATTR13_UNK24 = 0x01000000, // 24
        SPELL_ATTR13_UNK25 = 0x02000000, // 25
        SPELL_ATTR13_UNK26 = 0x04000000, // 26
        SPELL_ATTR13_UNK27 = 0x08000000, // 27
        SPELL_ATTR13_UNK28 = 0x10000000, // 28
        SPELL_ATTR13_UNK29 = 0x20000000, // 29
        SPELL_ATTR13_UNK30 = 0x40000000, // 30
        SPELL_ATTR13_UNK31 = 0x80000000  // 31
    }

    // 5.4.8: SkyFire_548 Unit.h CombatRating
    public enum CombatRating
    {
        CR_WEAPON_SKILL                   = 0,
        CR_DEFENSE_SKILL                  = 1, // Removed in 4.0.1
        CR_DODGE                          = 2,
        CR_PARRY                          = 3,
        CR_BLOCK                          = 4,
        CR_HIT_MELEE                      = 5,
        CR_HIT_RANGED                     = 6,
        CR_HIT_SPELL                      = 7,
        CR_CRIT_MELEE                     = 8,
        CR_CRIT_RANGED                    = 9,
        CR_CRIT_SPELL                     = 10,
        CR_HIT_TAKEN_MELEE                = 11, // Deprecated since Cataclysm
        CR_HIT_TAKEN_RANGED               = 12, // Deprecated since Cataclysm
        CR_HIT_TAKEN_SPELL                = 13, // Deprecated since Cataclysm
        CR_RESILIENCE_CRIT_TAKEN          = 14,
        CR_RESILIENCE_PLAYER_DAMAGE_TAKEN = 15,
        CR_CRIT_TAKEN_SPELL               = 16, // Deprecated since Cataclysm
        CR_HASTE_MELEE                    = 17,
        CR_HASTE_RANGED                   = 18,
        CR_HASTE_SPELL                    = 19,
        CR_WEAPON_SKILL_MAINHAND          = 20,
        CR_WEAPON_SKILL_OFFHAND           = 21,
        CR_WEAPON_SKILL_RANGED            = 22,
        CR_EXPERTISE                      = 23,
        CR_ARMOR_PENETRATION              = 24,
        CR_MASTERY                        = 25,
        CR_PVP_POWER                      = 26,
    }

    // 5.4.8: SkyFire_548 Unit.h UnitMods
    public enum UnitMods
    {
        UNIT_MOD_STAT_STRENGTH       = 0, // UNIT_MOD_STAT_STRENGTH..UNIT_MOD_STAT_SPIRIT must be in existed order, it's accessed by index values of Stats enum.
        UNIT_MOD_STAT_AGILITY        = 1,
        UNIT_MOD_STAT_STAMINA        = 2,
        UNIT_MOD_STAT_INTELLECT      = 3,
        UNIT_MOD_STAT_SPIRIT         = 4,
        UNIT_MOD_HEALTH              = 5,
        UNIT_MOD_MANA                = 6, // UNIT_MOD_MANA..UNIT_MOD_RUNIC_POWER must be in existed order, it's accessed by index values of Powers enum.
        UNIT_MOD_RAGE                = 7,
        UNIT_MOD_FOCUS               = 8,
        UNIT_MOD_ENERGY              = 9,
        UNIT_MOD_UNUSED              = 10, // Old UNIT_MOD_HAPPINESS
        UNIT_MOD_RUNE                = 11,
        UNIT_MOD_RUNIC_POWER         = 12,
        UNIT_MOD_SOUL_SHARDS         = 13,
        UNIT_MOD_ECLIPSE             = 14,
        UNIT_MOD_HOLY_POWER          = 15,
        UNIT_MOD_ALTERNATE_POWER     = 16,
        UNIT_MOD_DARK_FORCE          = 17,
        UNIT_MOD_CHI                 = 18,
        UNIT_MOD_SHADOW_ORBS         = 19,
        UNIT_MOD_BURNING_EMBERS      = 20,
        UNIT_MOD_DEMONIC_FURY        = 21,
        UNIT_MOD_ARCANE_CHARGES      = 22,
        UNIT_MOD_ARMOR               = 23, // UNIT_MOD_ARMOR..UNIT_MOD_RESISTANCE_ARCANE must be in existed order, it's accessed by index values of SpellSchools enum.
        UNIT_MOD_RESISTANCE_HOLY     = 24,
        UNIT_MOD_RESISTANCE_FIRE     = 25,
        UNIT_MOD_RESISTANCE_NATURE   = 26,
        UNIT_MOD_RESISTANCE_FROST    = 27,
        UNIT_MOD_RESISTANCE_SHADOW   = 28,
        UNIT_MOD_RESISTANCE_ARCANE   = 29,
        UNIT_MOD_ATTACK_POWER        = 30,
        UNIT_MOD_ATTACK_POWER_RANGED = 31,
        UNIT_MOD_DAMAGE_MAINHAND     = 32,
        UNIT_MOD_DAMAGE_OFFHAND      = 33,
        UNIT_MOD_DAMAGE_RANGED       = 34,
        UNIT_MOD_END                 = 35,
        UNIT_MOD_STAT_START          = 0,
        UNIT_MOD_STAT_END            = 5,
        UNIT_MOD_RESISTANCE_START    = 23,
        UNIT_MOD_RESISTANCE_END      = 30,
        UNIT_MOD_POWER_START         = 6,
        UNIT_MOD_POWER_END           = 23,
    }

    // 5.4.8: SkyFire_548 Unit.h SpellInterruptFlags
    [Flags]
    public enum SpellInterruptFlags
    {
        SPELL_INTERRUPT_FLAG_MOVEMENT     = 0x00000001, // why need this for instant?
        SPELL_INTERRUPT_FLAG_PUSH_BACK    = 0x00000002, // push back
        SPELL_INTERRUPT_FLAG_UNK3         = 0x00000004, // any info?
        SPELL_INTERRUPT_FLAG_INTERRUPT    = 0x00000008, // interrupt
        SPELL_INTERRUPT_FLAG_ABORT_ON_DMG = 0x00000010, // _complete_ interrupt on direct damage
    }

    // 5.4.8: SkyFire_548 Unit.h SpellChannelInterruptFlags
    [Flags]
    public enum SpellChannelInterruptFlags
    {
        CHANNEL_INTERRUPT_FLAG_INTERRUPT = 0x00000008, // interrupt
        CHANNEL_FLAG_DELAY               = 0x00004000,
    }

    // 5.4.8: SkyFire_548 Unit.h SpellAuraInterruptFlags
    [Flags]
    public enum SpellAuraInterruptFlags : uint
    {
        AURA_INTERRUPT_FLAG_HITBYSPELL               = 0x00000001, // 0    removed when getting hit by a negative spell?
        AURA_INTERRUPT_FLAG_TAKE_DAMAGE              = 0x00000002, // 1    removed by any damage
        AURA_INTERRUPT_FLAG_CAST                     = 0x00000004, // 2    cast any spells
        AURA_INTERRUPT_FLAG_MOVE                     = 0x00000008, // 3    removed by any movement
        AURA_INTERRUPT_FLAG_TURNING                  = 0x00000010, // 4    removed by any turning
        AURA_INTERRUPT_FLAG_JUMP                     = 0x00000020, // 5    removed by entering combat
        AURA_INTERRUPT_FLAG_NOT_MOUNTED              = 0x00000040, // 6    removed by dismounting
        AURA_INTERRUPT_FLAG_NOT_ABOVEWATER           = 0x00000080, // 7    removed by entering water
        AURA_INTERRUPT_FLAG_NOT_UNDERWATER           = 0x00000100, // 8    removed by leaving water
        AURA_INTERRUPT_FLAG_NOT_SHEATHED             = 0x00000200, // 9    removed by unsheathing
        AURA_INTERRUPT_FLAG_TALK                     = 0x00000400, // 10   talk to npc / loot? action on creature
        AURA_INTERRUPT_FLAG_USE                      = 0x00000800, // 11   mine/use/open action on gameobject
        AURA_INTERRUPT_FLAG_MELEE_ATTACK             = 0x00001000, // 12   removed by attacking
        AURA_INTERRUPT_FLAG_SPELL_ATTACK             = 0x00002000, // 13   ???
        AURA_INTERRUPT_FLAG_UNK14                    = 0x00004000, // 14
        AURA_INTERRUPT_FLAG_TRANSFORM                = 0x00008000, // 15   removed by transform?
        AURA_INTERRUPT_FLAG_UNK16                    = 0x00010000, // 16
        AURA_INTERRUPT_FLAG_MOUNT                    = 0x00020000, // 17   misdirect, aspect, swim speed
        AURA_INTERRUPT_FLAG_NOT_SEATED               = 0x00040000, // 18   removed by standing up (used by food and drink mostly and sleep/Fake Death like)
        AURA_INTERRUPT_FLAG_CHANGE_MAP               = 0x00080000, // 19   leaving map/getting teleported
        AURA_INTERRUPT_FLAG_IMMUNE_OR_LOST_SELECTION = 0x00100000, // 20   removed by auras that make you invulnerable, or make other to lose selection on you
        AURA_INTERRUPT_FLAG_UNK21                    = 0x00200000, // 21
        AURA_INTERRUPT_FLAG_TELEPORTED               = 0x00400000, // 22
        AURA_INTERRUPT_FLAG_ENTER_PVP_COMBAT         = 0x00800000, // 23   removed by entering pvp combat
        AURA_INTERRUPT_FLAG_DIRECT_DAMAGE            = 0x01000000, // 24   removed by any direct damage
        AURA_INTERRUPT_FLAG_LANDING                  = 0x02000000, // 25   removed by hitting the ground
        AURA_INTERRUPT_FLAG_NOT_VICTIM               = 0x01000003,
    }

    // 5.4.8: SkyFire_548 SharedDefines.h Classes
    public enum Classes
    {
        CLASS_WARRIOR      = 1,
        CLASS_PALADIN      = 2,
        CLASS_HUNTER       = 3,
        CLASS_ROGUE        = 4,
        CLASS_PRIEST       = 5,
        CLASS_DEATH_KNIGHT = 6,
        CLASS_SHAMAN       = 7,
        CLASS_MAGE         = 8,
        CLASS_WARLOCK      = 9,
        CLASS_MONK         = 10,
        CLASS_DRUID        = 11,
    }

// ReSharper restore InconsistentNaming

    public class SpellEnums
    {
        #region ProcFlagDesc

        public static readonly string[] ProcFlagDesc =
        {
            //00 0x00000001 000000000000000000000001 -
            "00 Killed by aggressor that receive experience or honor",
            //01 0x00000002 000000000000000000000010 -
            "01 Kill that yields experience or honor",

            //02 0x00000004 000000000000000000000100 -
            "02 Successful melee attack",
            //03 0x00000008 000000000000000000001000 -
            "03 Taken damage from melee strike hit",

            //04 0x00000010 000000000000000000010000 -
            "04 Successful attack by Spell that use melee weapon",
            //05 0x00000020 000000000000000000100000 -
            "05 Taken damage by Spell that use melee weapon",

            //06 0x00000040 000000000000000001000000 -
            "06 Successful Ranged attack(and wand spell cast)",
            //07 0x00000080 000000000000000010000000 -
            "07 Taken damage from ranged attack",

            //08 0x00000100 000000000000000100000000 -
            "08 Successful Ranged attack by Spell that use ranged weapon",
            //09 0x00000200 000000000000001000000000 -
            "09 Taken damage by Spell that use ranged weapon",

            //10 0x00000400 000000000000010000000000 -
            "10 Successful positive spell hit",
            //11 0x00000800 000000000000100000000000 -
            "11 Taken positive spell hit",

            //12 0x00001000 000000000001000000000000 -
            "12 Successful negative spell cast",
            //13 0x00002000 000000000010000000000000 -
            "13 Taken negative spell hit",

            //14 0x00004000 000000000100000000000000 -
            "14 Successful cast positive magic spell",
            //15 0x00008000 000000001000000000000000 -
            "15 Taken positive magic spell hit",

            //16 0x00010000 000000010000000000000000 -
            "16 Successful damage from harmful magic spell cast",
            //17 0x00020000 000000100000000000000000 -
            "17 Taken magic spell damage",

            //18 0x00040000 000001000000000000000000 -
            "18 Deal periodic damage",
            //19 0x00080000 000010000000000000000000 -
            "19 Taken periodic damage",

            //20 0x00100000 000100000000000000000000 -
            "20 Taken any damage",
            //21 0x00200000 001000000000000000000000 -
            "21 On trap activation",

            //22 0x00800000 010000000000000000000000 -
            "22 Successful main-hand melee attacks",
            //23 0x00800000 100000000000000000000000 -
            "23 Successful off-hand melee attacks",

            //24 0x01000000
            "24 On death",
            "25",
            "26",
            "27",
            "28",
            "29",
            "30",
            "31"
        };
        #endregion
    }
}
