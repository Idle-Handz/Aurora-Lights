# Character Fixture Coverage

`Characters/` contains sanitized `.dnd5e` files used for integration-style
load, save, and reload coverage. Keep these files free of player data, local
paths, and embedded portrait data.

Required sanitization:

- group: `Test Fixtures`
- empty player name and backstory
- empty portrait `local` and `base64` nodes
- no user-specific file paths or local content references

Current character fixtures:

| Fixture | Primary coverage | Main assertions |
| --- | --- | --- |
| `prepared-paladin.dnd5e` | prepared caster, armor, inventory, single-class advancement | restores Paladin level/progression, prepared spell ids, heavy armor registration, core snapshot save/reload |
| `prepared-domain-cleric.dnd5e` | manual prepared spells, always-prepared spells, combat snapshot, movement speeds | preserves 15 total Cleric prepared/always-prepared ids, identifies always-prepared spells separately, preserves combat/ability/spell state through save/reload |
| `multiclass-prepared-caster.dnd5e` | multiclass progression and prepared spells | restores Barbarian 3 / Druid 2 progression, advancement timeline groups, Druid prepared spell ids, core snapshot save/reload |
| `legacy-edited-arilith.dnd5e` | sanitized real Legacy-edited character file from the old character archive | loads an older `.dnd5e` shape with group/display metadata preserved and private data removed |

Fixture-backed tests:

| Test suite | Coverage |
| --- | --- |
| `CharacterFixtureParityTests` | full fixture load, sanitized fixture checks, snapshot save/reload parity, Legacy-edited group/name/core-state compatibility |
| `CharacterBuildingFlowTests` | database/content availability, fixture load smoke coverage, equipment custom name/notes save/reload, prepared spell save/reload, prepared spell toggle persistence, simple Human/Fighter/Soldier build round trip |
| `LegacyParityScenarioTests` | seed-based build parity scenarios for active selection rules, option availability, expected spellcasting profiles, and registered element/choice-row round trips |
| `SelectionRuleRegistrationTests` | real selection mutation and save/reload for language, list, spell, proficiency, ability-score, and repeatable invocation selections |
| `AdvancementTimelineQueryTests` | feature timeline grouping by granted class level without mutating progression state |
| `ChoiceIdentityFixtureTests` | translator-backed stable choice identity fixtures for same-label Paladin/Ranger Weapon Mastery rows |

`ParityScenarios/` contains small JSON seed definitions for lower-level build
states. These are preferable to full character files when the test only needs
to assert active choice rules, option availability, or unselected choice slots.

Current parity scenarios:

| Scenario | Primary coverage |
| --- | --- |
| `2014-human-fighter-acolyte.json` | 2014 race/class/background seed with fighting style, acolyte language, and optional background list choices |
| `2014-druid-cantrips-acolyte.json` | 2014 Druid spellcasting and cantrip selection options |
| `2014-variant-human-fighter-acolyte.json` | variant human feat and level-1 racial ASI selection slots |
| `2024-druid-acolyte.json` | 2024 class/background flow with cantrip, feat, and background ability-score choices |

`ChoiceIdentity/` contains translator character-state fixtures that document
the intended stable choice identity behavior. These fixtures are not full
Aurora-Lights app-contract round trips yet; they protect the matcher priority
for the known ambiguous Paladin/Ranger Weapon Mastery case:

1. `choiceRowKey`
2. `choiceKey`
3. `selectId`
4. legacy owner/select labels, only when unambiguous

Remaining gaps before this coverage should be considered complete:

- Optional: add a dedicated committed level-1 race/background ASI `.dnd5e`
  fixture if we want a static file for that case. The behavior is currently
  covered by a seed scenario that is serialized/reloaded during the test run.
- Add full app-contract save/restore coverage once Aurora-Lights models
  `choiceRowKey`/`choiceKey` on real choice rows instead of only testing the
  translator character-state fixture shape.
- Add a fixture with portrait/group metadata if a sanitized portrait case is
  needed; current sanitization intentionally removes embedded/local portrait data.
- Add more naturally Legacy-edited `.dnd5e` fixtures if we need wider coverage
  across higher-level characters, custom content, or older file variants.

When fixing a regression, add the narrowest fixture that reproduces the
behavior. Prefer a parity scenario for selection-rule behavior and a character
file when save/load or inventory/spell state is involved.
