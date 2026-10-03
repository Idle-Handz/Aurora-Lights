# Seven remaining differing-definition conflicts

| Conflict | Description / structure differences | Assessment |
| --- | --- | --- |
| Staff of Flowers | DMG copy uses Magic action wording, adds keywords, costs 100 gp, and uses Magic Weapons / Weapon with Staff addition. Xanathar copy uses action, costs 0 gp, uses Staffs / Staff, and specifies onehand. Both retain 10 charges and the same recharge/destruction behavior. | Revised text and modeling under an unchanged XGTE identity; source attribution needs review. |
| Adamantine / Walloping Ammunition | Adamantine makes hits against objects critical; Walloping forces DC 10 Strength or prone. Keywords and name-format differ. Both are uncommon, 400 gp, stackable ammunition. | Separate items sharing an ID. |
| Musketball (20) / Musketball | Identical description; bundle costs 2 gp and weighs 1 lb, single costs 1 sp and weighs 0.05 lb. Both stackable. | Pack versus single unit; values scale exactly by 20. |
| Defender of Kin / Slayer of Foes | Ally temporary HP versus extra damage on a successful attack, both using zealotry die and bonus action. Description and sheet differ. ID ends with an underscore. | Distinct feature choices with an apparently unfinished/copied ID. |
| Cloudstep / Heartening Breath | Bonus-action flight for 10 minutes, difficult terrain, once/long rest versus action, 30-foot cone, 1d4 bonuses for 1 minute with concentration, proficiency uses/long rest. Parent has two grants to the same duplicated ID. | Distinct traits; correcting definitions alone will not repair both grants. |
| Cimbalom / Kaba Gaida | Weather control and weather protection versus lightning/thunder spell enhancement and at-will Gust of Wind. Kaba has explicit ID_PHB_SPELL_GUST_OF_WIND description reference; setters match. | Separate instruments, not revisions. |
| Ancient Heart Strength +2 / +3 | Rules grant 2 versus 3 to strength and strength:max. Text also changes fatal roll from 2 to 3 and resulting monster CR from 20+ to 30+. Both hidden from compendium. | Separate mechanical variants; danger text needs author-source verification, not automatic normalization. |

Dates below are the installed Source elements' `release` metadata, not independently verified publication dates or XML commit dates. XML update versions are separate and are not comparable across different files. The installed XML does not record per-definition publication dates. Numeric row IDs refer to the existing, not-yet-refreshed v10 SQLite snapshot.

| Definition | Source and recorded release | Aurora ID (shared within conflict) | SQLite element_id | File / line and XML version |
| --- | --- | --- | ---: | --- |
| Cimbalom of Dazd | The Compendium of Forgotten Secrets; 2018-10-17 | `ID_GFP_COFSA_MAGIC_ITEM_WONDROUS_ITEM_CIMBALOM_OF_DAZD` | 16578 | [items.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/third-party/genuine-fantasy-press/forgotten-secrets/items.xml:756>), v0.0.1 |
| Kaba Gaida of Nevihta | The Compendium of Forgotten Secrets; 2018-10-17 | `ID_GFP_COFSA_MAGIC_ITEM_WONDROUS_ITEM_CIMBALOM_OF_DAZD` | 16580 | [items.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/third-party/genuine-fantasy-press/forgotten-secrets/items.xml:786>), v0.0.1 |
| Ancient Heart (Strength +2) | The Compendium of Forgotten Secrets; 2018-10-17 | `ID_GFP_COFSA_MAGIC_ITEM_WONDROUS_ITEM_ANCIENT_HEART_STRENGTH_TWO` | 16583 | [items.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/third-party/genuine-fantasy-press/forgotten-secrets/items.xml:835>), v0.0.1 |
| Ancient Heart (Strength +3) | The Compendium of Forgotten Secrets; 2018-10-17 | `ID_GFP_COFSA_MAGIC_ITEM_WONDROUS_ITEM_ANCIENT_HEART_STRENGTH_TWO` | 16584 | [items.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/third-party/genuine-fantasy-press/forgotten-secrets/items.xml:854>), v0.0.1 |
| Staff of Flowers | Xanathar’s Guide to Everything; 2017-11-21 | `ID_WOTC_XGTE_MAGIC_ITEM_STAFF_OF_FLOWERS` | 28257 | [items-staffs.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/core/dungeon-masters-guide-2024/items-staffs.xml:292>), v0.0.2 |
| Staff of Flowers | Xanathar’s Guide to Everything; 2017-11-21 | `ID_WOTC_XGTE_MAGIC_ITEM_STAFF_OF_FLOWERS` | 24030 | [items-wondrous.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/supplements/xanathars-guide-to-everything/items-wondrous.xml:512>), v0.0.7 |
| Adamantine Ammunition | Dungeon Master’s Guide (2024); 2024-11-12 | `ID_WOTC_DMG24_MAGIC_ITEM_AMMUNITION_ADAMANTINE` | 28270 | [items-weapons.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/core/dungeon-masters-guide-2024/items-weapons.xml:25>), v0.0.2 |
| Walloping Ammunition | Dungeon Master’s Guide (2024); 2024-11-12 | `ID_WOTC_DMG24_MAGIC_ITEM_AMMUNITION_ADAMANTINE` | 28337 | [items-weapons.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/core/dungeon-masters-guide-2024/items-weapons.xml:1332>), v0.0.2 |
| Musketball (20) | Arcane Artillery; Not recorded | `ID_RDDT_AA_MUSKETBALL` | 5009 | [guns.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/reddit/reddit-unearthed-arcana/arcane-artillery/guns.xml:571>), v0.0.1 |
| Musketball | Arcane Artillery; Not recorded | `ID_RDDT_AA_MUSKETBALL` | 5010 | [guns.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/reddit/reddit-unearthed-arcana/arcane-artillery/guns.xml:584>), v0.0.1 |
| Defender of Kin | Blazing Dawn Player’s Companion; 2020-08-12 | `ID_JONOMAN3000_ARCHETYPE_FEATURE_DEVOUT_ZEALOTS_DEVOTION_` | 5342 | [fighter-devout.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/reddit/reddit-unearthed-arcana/blazing-dawn-players-companion/fighter-devout.xml:59>), v0.0.1 |
| Slayer of Foes | Blazing Dawn Player’s Companion; 2020-08-12 | `ID_JONOMAN3000_ARCHETYPE_FEATURE_DEVOUT_ZEALOTS_DEVOTION_` | 5343 | [fighter-devout.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/reddit/reddit-unearthed-arcana/blazing-dawn-players-companion/fighter-devout.xml:67>), v0.0.1 |
| Cloudstep | Ryoko's Guide to the Yokai Realms; 2025-04-04 | `ID_RGTTYR_RACIAL_TRAIT_TATSUMI_RYUJIN_CLOUDSTEP` | 6735 | [race-tatsumi.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/ryokos-guide-to-the-yokai-realms/race-tatsumi.xml:272>), v0.1.0 |
| Heartening Breath | Ryoko's Guide to the Yokai Realms; 2025-04-04 | `ID_RGTTYR_RACIAL_TRAIT_TATSUMI_RYUJIN_CLOUDSTEP` | 6736 | [race-tatsumi.xml](<C:/Users/Ralla/Documents/5e Character Builder/custom/ryokos-guide-to-the-yokai-realms/race-tatsumi.xml:280>), v0.1.0 |

## Source identifiers

| Source | Source element Aurora ID |
| --- | --- |
| The Compendium of Forgotten Secrets | `ID_GFP_SOURCE_FORGOTTEN_SECRETS` |
| Xanathar’s Guide to Everything | `ID_WOTC_SOURCE_XANATHARS_GUIDE_TO_EVERYTHING` |
| Dungeon Master’s Guide (2024) | `ID_WOTC_SOURCE_DUNGEON_MASTERS_GUIDE_2024` |
| Arcane Artillery | `ID_RDDT_AA_SOURCE_ARCANEARTILLERY` |
| Blazing Dawn Player’s Companion | `ID_JONOMAN3000_BDPC_SOURCE_BLAZING_DAWN_PLAYERS_COMPANION` |
| Ryoko's Guide to the Yokai Realms | `ID_LT_SOURCE_RYOKOS_GUIDE_TO_THE_YOKAI_REALMS` |

The Staff of Flowers copy in the DMG 2024 file still declares Xanathar as its source. The containing DMG publication is recorded as 2024-11-12; the declared Xanathar source is recorded as 2017-11-21.
