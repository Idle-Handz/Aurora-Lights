# Content ID hotfixes, 2026-09-13

Subsequent update: the six local copies now contain embedded v1 correction
metadata, using verified originals as baselines. Ten operations remain protected
pending review; linked parent grants share groups. See
[the annotation/deployment record](hotfix-metadata-annotation-2026-09-13.md).
The original verification and pre-lifecycle limitations below are historical.

Six installed source files were corrected and verified, then copied byte-for-byte
to `C:\Users\Ralla\Documents\5e Character Builder\custom\user\local\id-hotfixes-20260913`,
preserving their relative source paths. Existing local files were not overwritten.
All originals, SHA-256 hashes, a manifest and the exact patch are preserved in
`C:\Users\Ralla\Documents\Aurora Content Archive\2026-09-13-id-hotfixes-originals`.

| Definition | Corrected identity / change |
| --- | --- |
| DMG Staff of Flowers | `ID_WOTC_DMG24_MAGIC_ITEM_STAFF_OF_FLOWERS`; source changed to `Dungeon Master’s Guide (2024)`. XGTE identity and references remain intact. |
| Walloping Ammunition | `ID_WOTC_DMG24_MAGIC_ITEM_AMMUNITION_WALLOPING` |
| Musketball | Retain the single-piece `ID_RDDT_AA_MUSKETBALL`, 1 sp / 0.05 lb; remove the 20-piece declaration. |
| Defender of Kin | `ID_JONOMAN3000_ARCHETYPE_FEATURE_DEVOUT_ZEALOTS_DEVOTION_DEFENDER_OF_KIN` |
| Slayer of Foes | `ID_JONOMAN3000_ARCHETYPE_FEATURE_DEVOUT_ZEALOTS_DEVOTION_SLAYER_OF_FOES` |
| Heartening Breath | `ID_RGTTYR_RACIAL_TRAIT_TATSUMI_RYUJIN_HEARTENING_BREATH`; Cloudstep retains its existing ID. |
| Kaba Gaida of Nevihta | `ID_GFP_COFSA_MAGIC_ITEM_WONDROUS_ITEM_KABA_GAIDA_OF_NEVIHTA`; Cimbalom retains its existing ID. |
| Ancient Heart Strength +3 | `ID_GFP_COFSA_MAGIC_ITEM_WONDROUS_ITEM_ANCIENT_HEART_STRENGTH_THREE`; +2 retains TWO. |

Devout's two empty level-3 grants now point to Defender and Slayer respectively.
Tatsumi's second duplicate Cloudstep grant now points to Heartening Breath.
No balance, description, or Ancient Heart danger-text corrections were made.

## Verification

- Parsed every staged XML file and asserted unique IDs in each fixed file.
- Verified requested renamed IDs and matching parent grants, and retention of only
  the single-unit Musketball.
- Imported the six files into a temporary SQLite database using the bundled
  Translator: 247 elements / 247 distinct Aurora IDs; no duplicate IDs.
- Verified the two Devout grants and Heartening Breath grant resolve to non-null
  target rows.
- Verified installed originals and local copies against the staged SHA-256 hashes.
- Re-scanned the installed corpus excluding user/local: only the five previously
  identified structurally identical duplicate groups remain; zero differing groups
  and zero XML parse errors. See [the scan](duplicate-hotfix-after.json).

The production database was not rebuilt and the app was not reloaded. No character
save files or ambiguous historical selections were rewritten. Full local copies
initially pinned all definitions in their respective files until retired; automatic retirement
when upstream matches remains a roadmap feature. Because the current database
reader/overlay is ID-based and does not implement file-replacement tombstones,
at that stage, reimports of unfixed upstream content could still leave obsolete base rows;
these copies are preserved hotfix material, not a completed lifecycle solution.

## Upstream PR follow-up

- AuroraLegacy/elements: DMG `items/items-staffs.xml` and `items/items-weapons.xml`.
- community-elements/elements-reddit: Arcane Artillery `guns.xml` and Blazing Dawn
  `fighter-devout.xml`.
- StrangeMeta/homebrew: Ryoko `Races/race-tatsumi.xml`.
- Forgotten Secrets file currently advertises aurorabuilder/elements; confirm its
  maintained successor before opening a PR for `forgotten-secrets/items.xml`.

An app reminder is scheduled for Saturday at 10:00 local time to review and submit
these PRs. It is instructed to pause after notifying once. No PR was submitted.

The roadmap now includes an Advanced-options conflict diff/resolution window
surfaced during database sync in the builder, potentially reusing
Constellations/Aurora XMLHelper/Aurora Studio logic for small local repairs.
