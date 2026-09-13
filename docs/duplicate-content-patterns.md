# Duplicate content patterns after excluding user/local

Primary custom-directory scan, 2026-09-13. Full group inventory and example IDs:
[duplicate-pattern-evidence.json](duplicate-pattern-evidence.json).

## Why source-qualified identity changed the count

- Original duplicate-ID groups: 1,682.
- Excluding `user/local`: 1,587 duplicate-ID groups.
- Of those, 1,501 have different source strings and 86 retain the same source.
- Every one of the 1,501 different-source groups is the same pairing:
  `The Book of Xellarant` versus `The Book of Beasts`.
- All 1,501 pairs are structurally equal after removing the root element's `source`
  attribute. Comparison ignores attribute order and surrounding whitespace, and
  preserves child order. This is not an assertion that source restrictions or
  reference resolution make the two identities interchangeable at runtime.

Example: `ID_XELLARANT_COMPANION_HOMUNCULUS` appears in both:

| File under custom | Source |
| --- | --- |
| `supplements/the-book-of-xellarant/creatures.xml` | The Book of Xellarant |
| `the-book-of-xellarant/creatures.xml` | The Book of Beasts |

Companion traits and actions retain the same IDs too, such as
`ID_XELLARANT_COMPANION_TRAIT_HOMUNCULUS_TELEPATHIC_BOND` and
`ID_XELLARANT_COMPANION_ACTION_HOMUNCULUS_BITE`.

Both files declare update version `0.0.2`. Their update URLs differ: the supplement
copy uses `main/creatures.xml`; the other uses
`master/the-book-of-xellarant/creatures.xml`. Their info labels also differ.
This is consistent with duplicated installation plus a source-label reorganization,
not independent authors accidentally reusing 1,501 IDs. History was not traced, so
the scan does not establish which installed file should be retained.

The newer-looking source label is not encoded into these IDs: `XELLARANT` remains
their namespace. Source labels and ID prefixes are therefore not mechanically
coupled. The composite key distinguishes these copies, but does not clean up the
duplicate installation. Its dramatic count reduction is not evidence of a general
need to preserve multiple distinct definitions under the same Aurora ID.

## Remaining 86 same-source duplicate groups

| Pattern | Groups | Structurally identical |
| --- | ---: | ---: |
| UA `20200204.xml` / `20200206.xml` | 28 | 25 |
| Tasha optional Ranger features / revised Ranger | 17 | 4 |
| Fizban Drakewarden / copied Drakewarden | 15 | 11 |
| PHB Ranger / revised Ranger | 7 | 0 |
| UA `20170116.xml` / `20170117.xml` | 7 | 5 |
| Other repeated declarations and reused IDs | 12 | 5 |
| Total | 86 | 50 |

The 2020 UA pair repeats IDs containing `UA20200206` in both files, including the
Source element itself. This resembles overlapping copies of the same publication,
with a few changes, rather than two publications receiving the same namespace.

The Ranger files retain existing source labels and IDs while copying or changing
their definitions. For example, Fighting Style differs in requirements; Spellcasting
differs in requirements, rules, sheet content, and description. The scan does not
determine whether each copy is a deliberate override or an outdated copy.

The smaller remainder includes clear ID reuse: Adamantine Ammunition and Walloping
Ammunition share the same ID in one DMG file. It also includes harmless repeated
declarations and copied background features. Changing the key to `(ID, source)`
does not resolve conflicts within the same source.

## Implication

Keep the revised identity policy provisional until this concrete source-label
reorganization is accounted for. A source alias or installation cleanup may be
appropriate here, but it must preserve source restrictions and saved references.
Do not automatically merge the two source identities or remove user files on the
basis of this scan. The remaining same-source differences still need explicit
classification regardless of the chosen key.
