# Content declaration precedence, superseded declarations and aliases — September 27, 2026

Supersedes the collision half of `content-conflict-policy-2026-09-24.md`, which described the
quarantine behaviour that shipped in Aurora.Content 0.7.0. Released in 0.9.0 (content data
version 17).

## Who owns an element id

Aurora Legacy does not merge duplicate declarations. In `DataManager.InitializeElementDataAsync` a
later custom file re-declaring an id does `coreElements.Remove(existing)` then `Add(new)`, so **the
last file to declare an id owns it outright**. Load order is the explicit ladder in
`DataManager.GetCustomFiles`:

`srd`, `system-reference-document`, `core`, `supplements`, `unearthed-arcana`, `third-party`,
`homebrew`, **any unrecognised directory**, `user` subdirectories, content-root files, files
directly under `user`.

Installed content packs live in unrecognised directories, so a pack overriding official material is
the intended mechanism rather than a conflict. Within a bucket Legacy walks a directory's own files
before descending into its subdirectories.

Preparation previously chose the alphabetically first relative path, which is the opposite answer
for every pack override, and retention then pinned whichever definition the database imported first
so an override could never take effect on a later refresh. `LegacyLoadOrder` now encodes the ladder;
use it rather than re-deriving an order.

## Retention

Retention exists to stop a broken supplier erasing a working definition. It applies **only** to ids
whose supplier became unreadable, never to settle a disagreement between readable declarations —
consulting it for every collision is what froze the first-imported definition.

## Superseded declarations

Every declaration of an id gets its own `elements` row. The ones that lost carry
`declaration_status = 'superseded'`, no texts, grants or selects, and `compendium_display = 0`;
nothing links to them, because only one definition's mechanics may apply to an id. They exist so a
disagreement between two books is inspectable as data — see `v_duplicate_aurora_ids`, which reports
each declaration with its package and an `is_winner` flag.

Consequence for anything querying the database: `SELECT ... FROM elements WHERE aurora_id = X` is
ambiguous. Filter `declaration_status = 'effective'` or join
`resolved_elements_cache.winning_element_id`. Every reader in the app already does.

## Aliases

Content declares `<alias id="OLD" target="NEW" />` to forward an id it has renamed.
`CuratedElementAliases` carries the same thing for ids that were never content ids — a misspelling,
or a build of upstream content that is gone — seeded after content so a content-declared alias for
the same id keeps precedence.

**A forwarding address is consulted only when the saved id resolves to nothing.** An id that still
exists is never redirected, so a book cannot capture an identity it does not own. Preparation
refuses an alias whose saved id is still declared, whose target nothing declares, that points at
itself, or that duplicates another, and records each rejection as an `alias` skip.

An alias **cannot** rescue an id that still exists but is no longer granted along a character's
saved path. That is a different loss: fix it by having the grant point at the id the character
actually saved, not by aliasing.

## Gate

`legacy-dump` plus `compare_projection_winners.py` in `tools/ContentDatabaseRehearsal` diff the
legacy loader's winner per id against the database projection. Behavioural disagreements must be
zero apart from the known generated builtin. See that README for expected numbers.
