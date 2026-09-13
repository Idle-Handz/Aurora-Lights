# Element identity and uniqueness feasibility

Sweep date: 2026-09-13. Scope: current Lights and sibling Translator source,
Translator's September 12 first-party audit database, the primary custom content
database, and all 1,188 XML files under the primary custom directory. Additional
configured directories and embedded resource definitions were not scanned.
No user content, existing databases, or selection/import behavior was modified.

## Conclusion

After the content cleanup, the accepted target is a one-to-one `aurora_id` /
`element_id` relationship for canonical elements, excluding the override layer.
This supersedes the interim composite-key proposal. The final nonlocal scan had
20,767 declarations, 20,762 distinct IDs, and five structurally identical repeats;
no remaining repeated ID needed source to distinguish different definitions.
Adding a unique index alone still requires importer/provenance migration, since
refresh/deletion currently assumes each definition belongs to one source file.

## Accepted rules after the sweep

- Canonical content identity is Aurora ID alone. Source remains attribution,
  filtering, and provenance information; it is not part of the canonical key.
  Different definitions claiming one ID require a verified revision relationship
  or explicit conflict resolution.
- Clearly harmless identical repeated declarations can be ignored silently,
  including duplicates within one file. Across files, retain provenance so removal
  of one copy does not remove content still provided by another.
- Files specifically authored under `user/local` are authoritative builder-time
  overlays and may be cached in SQLite in a separate override layer. Preserve the
  base definitions; do not treat local records as ordinary competing base imports.
  Their use must not require a full database rebuild or repository commit.
- Explicitly marked corrections stay pinned until direct review or a verified
  authoritative download exactly incorporates the complete correction/group and
  imports successfully. The automatic published-match path is agreed but pending
  implementation. Unchanged
  companion elements in full local copies may follow verified authoritative
  updates. Compare base, recorded baseline, and local content; never infer that an
  unmarked legacy edit is disposable. See [the correction policy](local-correction-policy.md).
- A matching installed file alone is incorporated/ready for review. Verified
  authoritative downloads may automatically accept a complete published repair;
  disk matches must not be promoted into publication evidence. Evaluate the base
  without the overlay; preserve unrelated local
  definitions and do not automatically delete mixed files.
- Once a local file has no remaining marked corrections and its entire effective
  content matches an available authoritative update, automatically retire the
  file's override and cache. Prevent reimport of retired files and preserve any
  still-unique local content; see the correction policy for retirement conditions.
- Match local full-definition overrides by ID, using origin and baseline
  fingerprints to disambiguate malformed declarations being repaired. New local elements remain
  available at runtime independently of any base database row. Recompute or invalidate runtime
  lookups affected by overlays rather than retaining stale database-derived data.
- Preserve functionally or referentially distinct choices. Matching names and
  descriptions do not establish equivalence.

Historical pre-cleanup evidence follows. Rechecking the primary XML directory with exact source-attribute strings and
excluding `user/local` removes 209 declarations. Of 22,387 remaining declarations,
22,301 `(ID, source)` pairs are distinct. There are 86 duplicate-pair groups:
50 structurally identical and 36 structurally different. No retained declaration
has a missing source string. These measurements do not yet resolve strings to
Source-element IDs or certify that all structural differences affect behavior.

Follow-up: [the duplicate-pattern analysis](duplicate-content-patterns.md) found
that all 1,501 groups separated by source are paired Book of Xellarant / Book of
Beasts copies that differ only in the source attribute after formatting
normalization. The count reduction is therefore a source-label/installation
pattern, not evidence of 1,501 distinct mechanical definitions. This finding must
inform the final identity and source-alias design.

The remaining design seam is an ID-only reference: grants, appends, and older
saved selections may not specify a source. Qualified selections must retain source
identity through registration/save/load; unqualified references with multiple
possible targets need an explicit ambiguity policy. Do not silently invent a
same-source preference, package ranking, or first-match rule. A local hotfix must
identify its target just as precisely when the ID is ambiguous.

Existing `RawUserXmlOverlayService` already applies XML after SQLite loading, but
currently scans all of `custom/user` and replaces/appends by ID alone. This is
broader than the new `user/local` rule. Preserve existing behavior until that
boundary is deliberately migrated. Separate previously imported local rows from
the base during migration, or deleted hotfixes could survive in the database. Reset/reload the
runtime overlay from the base snapshot when local files change or are removed.

## Measured content

| Input | Element rows | Distinct Aurora IDs | Duplicate-ID groups |
| --- | ---: | ---: | ---: |
| Translator v11 first-party audit database | 11,648 | 11,645 | 3 |
| Installed v10 content database | 22,593 | 20,911 | 1,682 |
| Current primary-directory XML | 22,596 | 20,914 | 1,682 |

The database timestamps and exact paths are recorded in
[the evidence](uniqueness-sweep-evidence.json); database and current XML counts are
separate observations, not an assertion that the older database is fully current.
All XML files parsed successfully. No duplicate group crossed element types in
the scanned XML, so type-scoped uniqueness would not eliminate these collisions.
Neither database contained case-only Aurora-ID collisions.

Creating `UNIQUE(aurora_id)` on independent in-memory copies of both databases
failed with `UNIQUE constraint failed: elements.aurora_id`. Original databases
were opened read-only.

Of the 1,682 XML duplicate groups, 69 matched structurally after ignoring attribute
order and surrounding whitespace. This comparison preserves child order but does
not certify runtime or referential equivalence. The remaining 1,613 groups are
not necessarily 1,613 gameplay conflicts: differences require classification.
1,501 groups occur between `supplements/the-book-of-xellarant` and the second
`the-book-of-xellarant/creatures.xml` installation. The headline count therefore
mostly reflects overlapping content installations, not unrelated books reusing IDs.

Concrete examples:

- `ID_INTERNAL_PHB24_FEATURE_REPLACEMENT_ROGUE_THIEF_SUPREME_SNEAK_STEALTH_ATTACK`:
  two structurally identical declarations in `rogue-thief.xml`.
- `ID_WOTC_DMG24_MAGIC_ITEM_AMMUNITION_ADAMANTINE`: Adamantine Ammunition and
  Walloping Ammunition use the same ID in one file, with different descriptions
  and setters. This needs a content identity repair, not a precedence preference.
- `ID_WOTC_XGTE_MAGIC_ITEM_STAFF_OF_FLOWERS`: declarations in both the DMG 2024
  and Xanathar files; current XML differs in description and setters.
- `ID_WOTC_CLASSFEATURE_RANGER_FIGHTING_STYLE`: core and revised Ranger copies
  differ in requirements. The core copy excludes the replacement feature; the
  revised copy lacks that requirement. Their descriptions match.
- `ID_WOTC_CLASSFEATURE_RANGER_SPELLCASTING`: core and revised copies differ in
  description, requirements, rules, and sheet data. The sweep does not establish
  whether each overlapping copy is intentional or stale.

## Architecture implications

- Both schema implementations use a central `elements.element_id` primary key,
  with a nonunique index on `aurora_id`; subtype tables reference `element_id`.
- Both importers insert changed/new declarations as independent rows. Translator
  deletes a changed source file and relies on cascading deletion of its element
  rows, then resolves cross-file links again from text IDs.
- A canonical element shared by several identical source copies therefore needs
  multiple provenance records. Removing one copy must not delete the element while
  another valid copy remains. An upsert alone does not implement that behavior.
- Lights' `DbElementLoader.QueryElements` reads `resolved_elements_cache`, which
  already contains one winner per Aurora ID. Character registration resolves by
  Aurora ID (`SelectionRuleRegistrationService`), and saved choice-row keys identify
  prompts/slots (`SelectionRuleIdentityService`), not definition versions.
- There is no need to persist database row numbers in characters to implement
  canonical identity. Preserving canonical numeric keys during updates would still
  simplify internal foreign-key handling.
- Package activation, deferred grants/selects, support links, and resolution caches
  must be migrated along with the writer. Do not replace conflict handling with
  insertion order or `INSERT OR IGNORE`; either would silently choose content.
- Define ID case/whitespace normalization explicitly. The absence of observed
  case-only collisions does not remove differences between current case-sensitive
  and case-insensitive consumers.

## Option equivalence

`BuildSelectionOptionResolver.DeduplicateOptions` groups by `(Name, Description)`,
combines source labels, and keeps one representative ID. It does not compare
rules, setters, requirements, spellcasting, or incoming references. The option
projection lacks enough information to prove complete equivalence.

A bounded XML comparison found 381 groups with matching name, type, and structural
description but different other child content, using the first encountered
definition for each ID. This is not an exact count of affected pickers: candidate
eligibility and rendered description projection were not evaluated. For example,
`ID_VISION_DARKVISION` and `ID_VISION_LESSER_DARKVISION` have matching prose but
different exclusion requirements, including a reference to the other ID.

Policy: preserve distinct Aurora IDs unless an explicit canonical alias mapping also
preserves all relevant references and saved choices. Compare complete definitions
before consolidating repeated copies of one Aurora ID. A content hash is useful for
detecting equality but is not, on its own, proof that two different IDs can be
interchanged. The safe immediate resolver change would remove name/description-only
collapse; verified canonicalization belongs before presentation.

## Proposed implementation sequence

1. Classify import declarations before modifying the active database: identical
   copies, authoritative revisions, and unresolved conflicting definitions. Preserve
   the last valid database if a refresh cannot resolve a conflict.
2. Add canonical identity/provenance handling; enforce unique nonempty
   `aurora_id` values on canonical elements. Retain conflict
   evidence separately, not as competing runtime elements of the same identity.
   Determine revision authority explicitly, not by file mtime. Move `user/local`
   imports into a separate override layer with persistent correction metadata.
3. Migrate old duplicate rows and foreign keys transactionally; preserve multiple
   source associations. Route all writers through the policy, including the copied
   importer until the separately planned shared-library migration removes it.
4. Keep Aurora IDs consistent through package/source activation, dependent links,
   runtime overlays, and character persistence. Migrate historical repaired IDs
   only when the intended target is unambiguous, then retire unnecessary winner ranking.
5. Remove unsafe option collapsing and verify selection/save/load identity. Cover
   equal text with different rules or references, identical repeated declarations,
   conflicting same-file identities, authoritative updates, deletion of one source
   copy, local override removal, and rollback of rejected refreshes with focused
   regression fixtures.

This is a coordinated schema/importer migration, not a uniqueness-index patch.
The sweep establishes feasibility and concrete blockers; it does not authorize
automatic repairs to user XML or choose among unresolved definitions.
