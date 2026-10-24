# Bundled content and asset distribution review

Reviewed 2026-10-23 on `feature_public-v1.0-build-preparation`.
Windows x64 and Android ARM64 are the stable targets; Windows ARM64 and macOS
remain experimental, and Web is deferred.

## Outcome

The reviewed content/asset licensing records are resolved for the public build
preparation baseline. Original application code remains MIT. Actual SRD-derived
material uses CC-BY-4.0; dependencies, fonts and legacy artwork retain their
separate terms. This conclusion uses the maintainer's explicit ownership and
redistribution confirmations recorded below, together with the actual SRD and
font/license sources. Private permission correspondence was not published.

Installed application data, characters, imported content, portraits and accounts
were not accessed or changed. Repository character fixtures remain unchanged.
No phone installation, signing, driver operation, VM, stress test, restart,
commit, push, tag or release publication was performed for this work.

## Final content boundary

The reviewed baseline has 72 XML resources and 1,854 elements, including twelve
classes, twelve SRD subclasses, nine races, four subraces, Acolyte, Grappler,
319 spells, 343 magic-item variants and 67 historical-pantheon deities.

- Removed 42 non-SRD spells. Seventeen spells use canonical SRD names while
  preserving their identifiers. Existing saved names resolve through those IDs.
- Restored ten truncated spell descriptions from the licensed SRD: Blight,
  Clone, Detect Thoughts, Flesh to Stone, Hallow, Imprisonment, Mirage Arcane,
  Raise Dead, Resurrection and Symbol. Corrected additional transcription errors.
- Corrected Wind Walk's casting time to 1 minute, a duration-unit typo and a
  duplicated material-component phrase. Canonical names also appear in related
  class tables and descriptions.
- Removed 27 non-SRD magic items and renamed eight retained item families to
  their SRD names, keeping their IDs. Scroll descriptions now use the SRD's
  casting-time, material-component, interruption and copying rules.
- Replaced 61 Forgotten Realms/nonhuman deity definitions with 67 licensed
  historical-pantheon entries from SRD pages 360-362. New IDs prevent silently
  reinterpreting an existing character's deity.
- Removed the Human variant and six helpers, five unused non-SRD elf traits,
  the Underdark circle option, and two gaming-set items with their proficiencies.
  The two associated aggregate grants were removed.
- Removed extra class/race and setting prose, creature lore, expanded tool
  activities/DC tables and book marketing copy. Tool descriptions now use the
  actual SRD rules. The twelve legacy help pages use original app guidance.
- Replaced copied name-suggestion lists with original suggestions while keeping
  their generator groups/formats. Corrected displayed sorcerer hit dice to d6,
  removed Grappler's obsolete third bullet absent from the licensed SRD, and
  aligned Infernal Legacy's description with the SRD's long-rest recharge.

Retained calculation/grant XML was compared before and after each text stage;
changes were limited to the documented exclusions and header correction. Exact
hashes, subjects, source sections and compatibility names are recorded in
`third-party/srd-baseline.json`, `srd-spells.json` and `CONTENT-SCOPE.md`.
The original 28 starting-equipment maps remain available for imported content.
Their ID/quantity/choice data contains no descriptive text nodes or artwork;
these are original MIT integration adapters, not bundled copies of optional
class or background definitions. The functional-code classification is an
inference informed by [Copyright Office Circular 33](https://www.copyright.gov/circs/circ33.pdf),
not a general license for copying book prose or artwork.

## Asset and notice inventory

`third-party/assets.json` records 652 file paths by hash: 291 conservatively
included in stable native resource sets and 361 additional source-only assets.
Counts identify paths, including duplicate copies, rather than unique works.

| Group | Paths | Distribution basis |
| --- | ---: | --- |
| Reviewed rules XML, including spells | 72 | CC-BY-4.0 for SRD material; MIT for original integration/guidance |
| Starting-equipment adapters | 28 | Original MIT integration data |
| Original help HTML and styles | 14 | MIT project material; copied help prose replaced |
| Historical OGL notice | 1 | Preserved unchanged; separate from the CC baseline |
| Legacy templates, galleries, portrait, icons and backgrounds | 523 | Explicit maintainer-confirmed redistribution permission |
| Native and documented branding | 8 | Confirmed maintainer creation and original project material |
| Standalone fonts | 6 | Five Open Sans files under Apache-2.0/OFL-1.1; Nodesto under CC0 |

Legacy permission covers complete templates with their embedded resources and
existing notices. It is not an MIT relicensing or a standalone font grant.
See `third-party/LEGACY-ASSET-PERMISSION.md`. The maintainer confirmed creating
Reflections with assistant help and creating the Constellations/Transpositions
working-title logos; see `BRANDING-PROVENANCE.md`.

The unused unverified Zelda font and two unused MAUI starter SVG icons are
excluded from the preparation source; the active PNG icon is unchanged.
The inventory now contains 204 dependency/content components and 55 generated
offline notice files. Matching LGPL library source and rebuild/relink materials
remain included. Imported content retains its own terms.

## Character loading and PDF acceptance

Final focused run: **136 passed, zero failed, zero skipped**. This covers the new
baseline, legacy-name spell and inventory loading, missing-content reporting,
character selection restoration, portable fonts, full-sheet export modes,
optional pages, card categories, image pages, flattening and PDF delivery-file
helpers. Existing nullable/analyzer warnings remain; no build/test error remains.

A synthetic character with an excluded spell ID was saved with an original
imported definition. Removing that definition produced a missing-content report
without changing the saved bytes. Restoring it allowed a successful load with
the same character name, ability score and spell ID. Additional round trips
checked older spell names and magic-item names/quantities. All tests use an
isolated temporary application context, not the installed app's directories.

All 319 spells were exported in each card mode: two 36-page decks. Each spell's
title and final description text were checked on its actual card page. The
exercise found and fixed formatted-card clipping: the exporter now measures the
same paragraphs/lists/tables it paints. Lists use one marker per item and retain
numbering. Long material requirements move into the readable body instead of
being squeezed into the header. Six dense-card examples were rendered and
visually reviewed, including Clone, Hallow, Imprisonment, Symbol and Wish.

Independent PDF checks cover all 32 modern/legacy sheet combinations: editable
canonical fields agree with widget values, and flattened sheets have no fields
or widget annotations. Spell cards remain painted text in both modes, as before.

## Distribution guards and source materials

- All **16 licensing guard regressions pass**. Asset changes cannot bypass the
  separate content/subject review merely by refreshing an asset hash. Original
  adapter classification is guarded against newly embedded descriptions.
- Strict notice/dependency checks pass for **79 Windows** and **165 Android**
  resolved package dependencies. The Windows check uses the earlier migrated
  engine's published dependency graph; it is not a new native package build.
- All **707 asset/notice files** preserve their reviewed bytes through Git's
  checkout normalization. Generated notices now consistently use LF while
  upstream legal-text bytes remain preserved in the hashed source records.
- The source bundler independently rejects unresolved components, issues,
  assets or notice/source hashes before creating an archive. It excludes private
  configuration, production signing workflow, keys, scratch/research output,
  historical oracle binaries and saved-character fixtures containing imported
  non-SRD descriptions. Those fixtures remain intact locally and are not needed
  to rebuild/relink the native app. Source-native PDF tests remain runnable.

Create the reviewed snapshot with:

```powershell
python tools/licensing/build_source_bundle.py --require-resolved --output artifacts/licensing-2026-10-23/final-acceptance/aurora-corresponding-source.zip
```

The archive's `SOURCE-MANIFEST.json` records the actual included source hashes,
including local changes; a Git revision alone does not identify this snapshot.
Local evidence is under `artifacts/licensing-2026-10-23/final-acceptance/` and is
excluded from publication. Rebuild/relink instructions are in
`third-party/LGPL-RELINKING.md`.

## Release boundary

This completes the reviewed baseline/licensing work and focused shared-engine
acceptance, not the final native release. The installed Android app and earlier
Windows/Android acceptance packages predate these resources/layout fixes.
Commit/freeze a new candidate, then build and accept its actual Windows x64 and
signed Android ARM64 packages before publication. Windows signing/distribution
acceptance remains separate. No new physical-device acceptance is claimed here.

See [the user-facing migration guide](v1-public-baseline-migration.md) for opening
characters that require excluded content. The historical RC1 branch remains at
its original commit; this work stays local and uncommitted on the public build
preparation branch.

## Primary sources

- [Wizards' SRD scope and downloads](https://www.dndbeyond.com/srd).
- [Actual SRD 5.1 CC source](https://media.dndbeyond.com/compendium-images/srd/5.1/SRD_CC_v5.1.pdf).
- [CC-BY-4.0 legal code](https://creativecommons.org/licenses/by/4.0/legalcode.en).
- [CC0 legal code](https://creativecommons.org/publicdomain/zero/1.0/legalcode.en).
- [Original integration/functionality boundary](https://www.copyright.gov/circs/circ33.pdf).
