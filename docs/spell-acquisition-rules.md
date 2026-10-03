# Spell acquisition and casting contract

Approved implementation, 2026-09-16. This supplements the Translator handoff; the application owns character-specific spell resolution.

## Rules

- A spell definition and an acquisition of that spell are different things. Preserve each active grant/select origin. Removing one origin must not remove another acquisition of the same spell.
- Resolve a class profile using explicit `spellcasting` metadata first, then an unambiguous active Class/Class Feature/Archetype/Archetype Feature acquisition chain. Never choose the first caster or infer ownership from spell-list/support membership. Feats and racial features do not inherit class ownership just because a class selected them.
- Distinguish feature access, known spells, ordinary preparation, always-prepared spells, spellbook entries, ritual-only access, and list expansion. Expanding a list alone neither acquires a spell nor prepares it.
- Separate class-slot permission, permission to use any available slots, free uses, and limited uses that also consume a slot. A cantrip needs no slot.
- Class preparation still applies to the class casting route. A feature's free casting route can remain available independently. A different class having the spell on its list does not confer ownership or permission.
- Preserve casting ability by acquisition. Unknown metadata stays visible with its origin and a review note; it does not invent unlimited casting, slot permission, or ritual casting.
- Use source ID plus profile name to key prepared state. Runtime profile GUIDs are not durable. Existing character XML already identifies spellcasting by source and name; no save-format migration is required.
- Rebuild derived casting permissions from current content and saved build selections; do not persist guessed permissions into character files.

## Versioned compatibility

These are exact element-ID adapters for existing XML, not description keyword matching:

| Origin | Behaviour |
| --- | --- |
| PHB 2014 Magic Initiate class choices | Cantrips and one level-1 free casting per long rest, using the chosen class's ability. Matching class rules can authorize slots. Prepared classes still require preparation. Wizard feat acquisition does not automatically transcribe a spell into a spellbook. |
| PHB 2024 Magic Initiate class choices | Selected ability, always-prepared level-1 spell, one free casting per long rest, and permission to use available slots. No class ownership is inferred from the chosen list. |
| PHB 2024 Fiend Spells | Always prepared for the associated Warlock; no ordinary preparation/known capacity consumed. The 2014 expanded list remains eligibility only. |
| Necrotic Affinity | Acquired spells follow the Sorcerer acquisition chain and do not consume ordinary known capacity. The content's missing spell-level eligibility restriction remains a separate authoring correction. |
| Vile Heresies | One casting per long rest requiring a Warlock slot, not a free casting or an unrestricted known spell. Preserve the Archive exchange restriction as an explanatory note. |

## Structured content metadata

The application accepts these optional attributes on `grant` and `select` rules. Preserve them in canonical XML and prepared/effective XML in Translator imports; do not resolve them against a particular character in the importer.

| Attribute | Values / meaning |
| --- | --- |
| `spellcasting` | Existing class/profile association; also accepts `source-id|profile-name` for explicit disambiguation. |
| `prepared` | Existing `true` means always prepared; does not by itself authorize slots without a class association. |
| `spell-access` | `feature`, `known`, `prepared`, `always-prepared`, `spellbook`, `list`, `ritual`. |
| `spell-slots` | `none`, `class`, `any`; `class` requires a resolved profile. |
| `spell-ability` | Casting ability, independently of the spell's list. |
| `spell-uses` | Nonnegative free-use count. |
| `spell-slot-uses` | Nonnegative limit for casts that also consume slots. |
| `spell-recharge` | Displayed recharge condition, e.g. `Long Rest`. |
| `spell-counts-known` | Whether acquisition consumes ordinary known-spell capacity. |

Example: `spell-access="always-prepared" spell-slots="any" spell-ability="Wisdom" spell-uses="1" spell-recharge="Long Rest"`.

The application displays these restrictions; this change does not introduce a spell-casting transaction or automatically debit a feature-use counter. Slot toggles retain their existing manual behaviour. Unknown homebrew features still need authored metadata or a reviewed compatibility adapter. Do not add these application-specific attributes to upstream content without considering other readers.

## Integration and validation

The shared resolver feeds Reflections' spell lists, feature display, character magic serialization, and PDF spell pages. Class lists no longer receive every acquired spell when the character has only one caster. Distinct spell IDs are not collapsed by name. Selection registration permits separate acquisition domains, and progression cleanup preserves their origins.

Focused coverage includes edition-specific Magic Initiate, foreign-list permissions, noncaster subclass cantrips, class-chain resolution, ambiguous profiles, ritual-only access, list expansion, limited slot casts, independent origins, UI permission preservation, and the actual MAUI prepared-state handler.

Manual review: check a noncaster feature cantrip; 2014 versus 2024 Magic Initiate; a prepared class plus a feat's free casting; multiclass spells with overlapping lists; the same spell from two origins followed by removal of one; save/reload of prepared spells; feature spell PDF pages. Missing ability/usage notes identify content that needs review.

Validation completed:

- 60 focused tests passed, including rendered Magic workspace tests, actual MAUI preparation storage, typed duplicate spell registration and owner removal, option resolution, and existing selection save/reload tests. Results: `buildtmp/spell-rules-test-results/spell-rules.trx`.
- Windows MAUI build succeeded with zero warnings/errors: `buildtmp/spell-rules-app-build.log`.
- Isolated v12 character rehearsal preserved abilities, choices, inventory, and prepared state for Test E, Art E, Testy Guard edit, and Testy McGee. Original disposable copies were unchanged. Results: `buildtmp/refreshed-proficiency-review-20260916/spell-rules-characters-result.json`. The overall rehearsal status remains false because the original Art E and Testy copies contain the already-documented stale saved-element references; no new missing grants or lost choices were found. Test E and the clean Guard copy loaded successfully on both passes.
- No installed database refresh, live save edits, commit, or publication performed. Native UI review and visual PDF inspection remain outstanding.
