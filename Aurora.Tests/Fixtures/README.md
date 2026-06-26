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

- `prepared-paladin.dnd5e`: prepared caster, armor, and inventory state
- `prepared-domain-cleric.dnd5e`: manual and always-prepared spells, fly speed,
  and combat state
- `multiclass-prepared-caster.dnd5e`: multiclass progression and prepared spells

`ParityScenarios/` contains small JSON seed definitions for lower-level build
states. These are preferable to full character files when the test only needs
to assert active choice rules, option availability, or unselected choice slots.

When fixing a regression, add the narrowest fixture that reproduces the
behavior. Prefer a parity scenario for selection-rule behavior and a character
file when save/load or inventory/spell state is involved.
