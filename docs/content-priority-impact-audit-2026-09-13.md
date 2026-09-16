# Importer priority impact audit — September 13, 2026

## Finding

The 51 changed priority values do not change any Aurora-ID winner in the tested
corpus. They do change four inferred parent relationships: Blazing Dawn artificer
infusions become associated with the Unearthed Arcana version of **Infuse Item**
instead of the published Eberron version. This is a real relationship discrepancy,
but no current Lights choice-list or character-rule regression was demonstrated.

## Controlled comparison

Used the frozen 1,189-file candidate from the earlier full-refresh rehearsal,
containing 20,973 stored elements and 20,873 distinct Aurora IDs. No new content
import or XML changes were introduced into this comparison.

Two SQLite backups contained identical content, package kinds, enabled flags,
source-file assignments and row IDs. One retained the importer-produced ranks;
the other restored only the 51 previous ranks recorded during the earlier audit:
49 supplement records 400 to 200, UA 500 to 200, and user content 500 to 400.

Ran the actual bundled `AuroraTranslator.exe refresh-package-resolution` against
each temporary database, then compared every row in all 61 application tables.
Both outputs passed SQLite integrity and foreign-key checks. The original frozen
candidate and live database/XML were read-only inputs, never output targets.

Bundled `AuroraTranslator.dll` SHA-256:
`6228a685515b0772e3e85d9c1d167d24e9cf9065fcdebe0fc106601489ca5fb9`.
Scripts, JSON evidence, table differences and both temporary databases are under
`tmp/priority-impact/` (ignored diagnostic artifacts).

| Check | Result |
| --- | --- |
| Enabled Aurora-ID winners | All 19,808 identical |
| Winners with all packages enabled | All 20,873 identical |
| Duplicate-definition pairs | All 100 retain the same ordering |
| Changed package records | 51; only rank differs between test inputs |
| Cached rank values | 5,935 differ; cached winning IDs do not |
| Feature parent relationships | Four differ |
| Primary-parent support links | The same four differ |
| Other application tables | All 57 identical, including grants, selections, rules, texts and correction mirrors |
| Four infusions' selectable-option memberships | All 40 identical: 20 under Eberron selections, 20 under Tasha selections |
| Class progression, background/race core and unresolved-link views | Identical |

Pairwise duplicate ordering also covers any subset of enabled suppliers for these
same stored declarations. It does not predict the behavior of future content.

## The four changed relationships

All four are Class Features from **Blazing Dawn Player’s Companion**, in
`reddit/reddit-unearthed-arcana/blazing-dawn-players-companion/artificer-infusions.xml`.

| Feature | Aurora ID |
| --- | --- |
| Arcane Cord | `ID_JONOMAN3000_BDPC_CLASS_FEATURE_ARTIFICER_INFUSION_ARCANE_CORD` |
| Elemental Focus | `ID_JONOMAN3000_BDPC_CLASS_FEATURE_ARTIFICER_INFUSION_ELEMENTAL_FOCUS` |
| Focused Lightblade | `ID_JONOMAN3000_BDPC_CLASS_FEATURE_ARTIFICER_INFUSION_FOCUSED_LIGHTBLADE` |
| Helm of Heat-vision | `ID_JONOMAN3000_BDPC_CLASS_FEATURE_ARTIFICER_INFUSION_HELM_OF_HEAT_VISION` |

Before: `ID_WOTC_ERLW_CLASS_FEATURE_ARTIFICER_INFUSE_ITEM`, **Eberron: Rising from
the Last War** (row 8155 in this frozen fixture).

After: `ID_WOTC_UA20190228_CLASS_FEATURE_ARTIFICER_INFUSE_ITEM`, **Unearthed Arcana:
Artificer Revisited**, 2019-02-28 content file (row 17439).

The alias table maps `Artificer Infusion` to a Class Feature named `Infuse Item`
without an explicit Aurora ID. The parent resolver considers same-file/package
affinity, cache membership, rank, alias priority and finally numeric element ID.
Neither parent shares the Blazing Dawn file/package. Previously the parent ranks
tied and the Eberron row won the final row-ID tie-break. Afterward UA's 500 outranks
Eberron's 400. Thus preserving the old values restores the earlier result, but
does not make the underlying inference semantically unambiguous.

These parents are not equivalent. The published Eberron selections use
`Artificer Infusion, !TCOE Base`, while UA selections use `UA Artificer Infusion`;
their level schedules and stat names differ too. The Blazing Dawn options declare
`Artificer Infusion`. Their own XML/rules and selectable support links are unchanged.

## Runtime and release implications

The current Lights `DbElementLoader` reads `features.min_level`, not
`features.parent_element_id`, and does not consume `element_support_links`.
Its raw supports, choices, grants and selected element definitions are identical
between the two outputs. The `v_feature_loader` and `v_element_support_loader`
views expose the four changes, so consumers relying on inferred parent metadata
can see different associations. This audit does not claim an end-to-end character
build test or demonstrate that the current Translator character engine triggers
a gameplay error from these four links.

Source classification is a related but separately scoped concern. The Translator
path-based derivation maps `supplements` to `homebrew`, and UA/user/reddit roots to
`local`; Lights also has source-element-based classification. Settings displays
package kind and sorts by it, so classification differences can affect labels and
ordering. This rank-only experiment deliberately holds kind/assignment constant.

Recommendation: do not present this as 51 broken sources or a demonstrated blocker
for the progress-display commit. Preserve existing ranks as a compatibility measure,
and fix/test parent-family inference before treating those links as authoritative
in the shared library. Match support/edition context and explicit identities;
represent unresolved or multiple compatible parents rather than silently selecting
a different edition by name and global rank. Canonical Aurora-ID uniqueness alone
cannot solve this case because the two parent definitions have different IDs.

No importer code, bundled executable, live content or live database was changed
by this audit. The broader minor-release assessment should track this concrete
relationship/classification issue rather than the count of changed numbers alone.
