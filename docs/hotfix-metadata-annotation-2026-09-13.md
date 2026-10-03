# Six-hotfix metadata annotation

Completed September 13, 2026. The six installed files under
`C:\Users\Ralla\Documents\5e Character Builder\custom\user\local\id-hotfixes-20260913`
now carry embedded correction metadata v1. All ten operations are
`review-pending`; no correction was accepted, retired, or deleted.

| File | Explicit operations | Review grouping |
| --- | --- | --- |
| DMG 2024 `items-staffs.xml` | Rename DMG Staff of Flowers, including corrected source attribution | One operation |
| DMG 2024 `items-weapons.xml` | Rename Walloping Ammunition | One operation |
| Arcane Artillery `guns.xml` | Remove Musketball (20), preserve single piece | One operation |
| Devout `fighter-devout.xml` | Rename Defender of Kin and Slayer of Foes; replace their parent definition to repair two grants | `hotfix-20260913-fighter-devout` |
| Tatsumi `race-tatsumi.xml` | Rename Heartening Breath; replace its parent definition to repair the grant | `hotfix-20260913-race-tatsumi` |
| Forgotten Secrets `items.xml` | Rename Kaba Gaida and Ancient Heart Strength +3 | Two independent operations |

Every operation carries an original-declaration fingerprint and explanation.
The complete original file is embedded as escaped XML baseline text. Original
archive, installed fixed source, and unannotated local hashes were checked against
the earlier hotfix manifest before staging and again before installation.
The annotation changes metadata only; all 247 gameplay declarations remain as
previously repaired.

## Authority and deployment

The nonlocal installed files already contain our earlier repairs. Therefore the
local comparison can report "incorporated" even though no upstream author PR has
been merged. This does not authorize automatic acceptance/retirement. The original
update URLs and file baselines are retained for later upstream review.

Recoverable pre-annotation local copies and the exact staging evidence are stored
at `C:\Users\Ralla\Documents\Aurora Content Archive\2026-09-13-metadata-annotation`.
The `annotation-evidence.json` there records original/fixed/annotated SHA-256
values, target paths, operations, review reasons, and test database evidence.
Its `installedContentModified: false` field describes the staging run; installation
then succeeded and all six installed hashes/evaluations were verified separately.

No production SQLite refresh or app reload/deployment was performed. The mirrored
six-file/ten-operation result was verified in a temporary DB. Production tracking
will populate on the next successful sync using the updated Lights code. The
code remains uncommitted/unpublished; the XML annotations are installed.

## Runtime issue fixed before installation

An old ID can remain legitimate in another file: DMG Staff of Flowers originally
reused the XGTE ID and even its source label. Removing that ID globally during
runtime overlay application could remove the real XGTE Staff.

Runtime DB/XML loading now carries file provenance, and correction suppression
matches that origin as well as the ID. The cached correction result includes its
resolved origin path. Unknown provenance is not treated as a match. This is a
runtime correction fix, not the still-pending normalized canonical/provenance
schema migration. It does not use source labels as a new canonical key.

## Verification

- Both archived unfixed inputs and current fixed installed inputs evaluate to the
  intended gameplay for all six files. No unclassified changes remain.
- All ten operations remain pinned; none of the files can automatically retire.
  Simulated explicit acceptance against matching inputs allows retirement.
- Actual bundled Translator, through `LocalCorrectionSync`: **248 elements,
  248 distinct IDs**, comprising the 247 hotfix-file definitions plus the legitimate
  XGTE Staff. Six mirror records, ten review-pending operations, three repaired
  grants resolved, fresh input hashes; candidate integrity/FK validation passed.
- **21 tests passed**: 19 lifecycle tests and two provenance regressions, covering
  the cross-file Staff suppression and primary/additional-root path resolution.
- Windows app build: **zero warnings, zero errors**.
- Post-install readback: all six hashes match the verified staged files, all ten
  operations evaluate as review-pending, with no unclassified changes or retirement.

Reproducible staging/verification tool:
`tools/AnnotateContentHotfixes/AnnotateContentHotfixes.csproj`. The tool never edits
installed files; its separate `Install.ps1` performs six preflight-checked atomic
file replacements with recoverable backups. Normal staging intentionally refuses
already-annotated/changed inputs rather than stacking another metadata section.

See [the maintained handoff](../../5eApiTranslator/docs/aurora-translator-data-handoff.md) and
[remaining work estimates](content-migration-estimates.md).
