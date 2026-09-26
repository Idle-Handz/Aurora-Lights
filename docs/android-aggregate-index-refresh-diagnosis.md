# Aggregate index refresh failure — September 25, 2026

## Reproduction

Reported on Android with Aurora.Content 0.7.1 after clearing app data and recursively downloading:

https://raw.githubusercontent.com/Xellarant/the-book-of-xellarant/master/aurora-sources.index

The live index was inspected. It includes the archived aurorabuilder core, supplements, unearthed-arcana and third-party indexes alongside AuroraLegacy, Reddit and D&D Wiki. Reinstalling these inputs restores the same overlaps. The previous Common language conflict is one such overlap.

The new error reports conflicting official, third-party and homebrew classifications for `aurora-sources/aurorabuilder-core/dungeon-masters-guide/class-oathbreaker.xml`.

## Confirmed library defect

The original and AuroraLegacy DMG Source declarations share `ID_WOTC_SOURCE_DUNGEON_MASTERS_GUIDE` but differ in description, image, core/supplement flags and release metadata. Both identify Wizards of the Coast and set `official=true`.

First-install preparation excludes the conflicting Source identity from the effective catalog, as intended. `ContentPackageClassification.ForCatalog` then cannot find its classification evidence. The package-key calculation groups all descendants of the unfamiliar `aurora-sources` root into one package. Its fallback at `ContentPackageClassification.cs:72` uses every Source declaration in that package, including unrelated independent and community publications. The mixed categories throw at line 80 and abort the import.

This diagnostic does not establish contradictory publisher flags inside Oathbreaker. It demonstrates a failure to contain the effect of an unavailable Source record, contrary to the intended first-install behavior of importing unaffected definitions.

## Verification

An isolated .NET harness against the **vendored 0.7.1 package** reproduced the exact error, first with synthetic records, then with the current public original Oathbreaker XML and both DMG Source XML files. Minimal unrelated official, third-party and homebrew Source fixtures were included under sibling collection directories. Removing only the second DMG Source from the disposable fixture allowed the same Oathbreaker file to import: 12 elements, zero skips. This was not a full import of the master index or an Android device run.

Local harness and downloaded XML: `buildtmp/android-content-classification/`; output: `upstream-repro.log`. No installed content, character files, live databases, index repositories or Translator sources were changed during diagnosis.

## Recommended follow-up

- Fix classification in the shared library, including collection boundaries and missing/quarantined Source evidence. Do not infer a file's publisher from unrelated siblings. Preserve source provenance without reactivating conflicted definitions or choosing an arbitrary winner.
- Cover the nested aggregate layout, first-install Source quarantine and ordinary missing Source metadata in regressions. Keep explicit contradictory publisher flags distinguishable from unrelated evidence.
- Review the master index's overlapping generations. Prefer one maintained baseline for overlapping content; retain archived collections only where their distinct content is intentional. Do not remove whole archived collections without checking which unique content would be lost.
- Vendor a new immutable package version and rebuild the app once the library repair is tested. Content cleanup alone does not repair the classification defect.

Removing an installed `.index` only unregisters it; it does not remove its downloaded XML. A cleanup must also address already downloaded duplicates and any parent index that would reintroduce them. Repeated app-data resets are not a remedy for this combination of inputs and library behavior.

## Requested policy revision

The user subsequently clarified that valid newer changes published by an authoritative upstream should become canonical automatically, and upstream definition conflicts should not necessarily block a skip-enabled refresh. The September 24 blanket rejection of new conflicting definitions is therefore under revision; the shipped 0.7.1 behavior described above has not yet changed.

Distinguish an ordinary update replacing a file (already accepted) from simultaneous differing suppliers of one ID (currently treated as a conflict). Automatic revision resolution needs an established upstream identity or successor relationship, not filesystem timestamps, download order, arbitrary repository URLs, or a globally compared XML version number. Publication metadata may also be used for display ordering and is not itself proof of revision order.

Two implementation choices were presented for confirmation:

1. Treat AuroraLegacy as the authoritative successor to the archived aurorabuilder collection for overlapping definitions.
2. For genuine ambiguity with no established authority, let skip-enabled refresh retain the last-known working definitions for affected IDs while updating unaffected content; leave affected IDs unavailable on first installation.

Explicit local correction protection remains in place. Do not change the UI explanation to advertise the revised behavior before the shared library implements and verifies it. The classification exception bypassing file/append skip handling remains a separate defect to fix.

## Approved implementation

Both succession and selective preservation are approved. The first-install policy was revised further: choose a deterministic valid provisional declaration and report the alternatives instead of leaving ordinary exact-ID collisions unavailable. See [the September 25 implementation record](content-import-resilience-2026-09-25.md). The historical reproduction above describes 0.7.1; the repaired shared library is 0.8.0 / data 14.
