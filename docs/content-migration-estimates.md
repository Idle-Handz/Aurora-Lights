# Remaining content work: effort estimates

Estimated September 13, 2026 from the current working tree. These are engineering
planning ranges, not measured completion times or a promise of elapsed agent time.
Assume one developer familiar with these repositories and an eight-hour workday.
Include implementation, focused regression coverage, and integration review.

| Deliverable | Effort | Working days | Confidence |
| --- | ---: | ---: | --- |
| Advanced diff/resolution UI, complete bounded builder workflow | 40–72 hours | 5–9 | Medium |
| Canonical uniqueness migration across active writers/readers | 64–112 hours | 8–14 | Medium-low |
| Combined | 104–184 hours | 13–23 | Approximately 3–5 working weeks before scheduling interruptions |

The estimate excludes shared-library packaging/extraction, Android deployment
validation, upstream PR turnaround, and a full authoring studio. Those are
separate deliverables. Existing data cleanup reduces known-content uncertainty;
it does not remove schema ownership/deletion or migration risks.

Subsequent progress: the neutral typed provenance/review foundation and read-only
classifier are implemented, with 16 focused tests. The responsibility boundary is
now explicit: app/CLI content preparation resolves inputs; SQLite writing validates
and persists the finalized result. AuroraXMLHelper diagnostics/repair previews
were inspected for reuse. This completes a small part of the model work, not the
full reference/provenance migration, production adapters or resolution API. See
[the contract](content-preparation-contract.md); the original ranges are retained
as planning context rather than subtracting unmeasured effort.

## Advanced diff/resolution UI

| Work | Hours |
| --- | ---: |
| Review model/API: baseline/local/upstream, correction groups, origin, affected references, structured sync conflict details | 8–14 |
| Responsive three-way comparison, XML/definition differences, navigation and Advanced-mode entry points | 12–20 |
| Keep local, edit/validate, accept upstream; stale-review handling, linked repairs and atomic multi-file application | 12–22 |
| Focused interaction tests, accessibility/error states, actual builder smoke verification | 8–16 |

Already available: XML evaluator, database mirror, cached effective XML, review
states/reasons, single-file `AcceptUpstream`, stale hashes, and Settings status
rows. Missing: interactive diff/editor, structured general-collision intake,
reference presentation, and atomic multi-file review. The evaluator rejects mixed
group states during sync; that does not provide a multi-file review transaction.

A smaller first slice could take **16–28 hours**: inspect the three versions for
existing marked corrections, show linked operations/references, keep the current
correction, or explicitly accept upstream within one file. This is useful early,
but excludes arbitrary XML editing, new canonical-conflict repair creation, and
multi-file atomic review, so it is not the finished advanced UI.

## Canonical uniqueness migration

| Work | Hours |
| --- | ---: |
| Canonical/declaration provenance design, exact ID normalization and conflict/revision classification | 10–18 |
| Transactional schema/backfill migration with old-row and dependent-FK mapping | 16–28 |
| Import/update/delete, duplicate suppliers, source/package availability, cache/deferred-link maintenance across active writer routes | 20–34 |
| Resolver/save-load identity regressions, migrated snapshot fixtures, rejected-refresh rollback and integration verification | 18–32 |

The current schema associates elements with one source file, and deleting that
file cascades through its element data. Both writers insert independent rows;
resolution caches rank competing IDs. A unique index alone would break imports
without solving shared suppliers, file deletion, availability, or references.
The migration also needs to preserve the distinct local override layer and
replace the option resolver's current name/description collapse.

Primary uncertainties: authority when future downloads introduce new conflicts,
multiple suppliers with different enablement states, ambiguous historical saved
IDs, and keeping the copied writer plus Translator aligned until extraction.
Do not guess historical character intent to make a migration appear complete.

## Suggested order

1. Deliver the small review UI against the existing correction API if immediate
   visibility is the priority.
2. Implement canonical identity/provenance and structured conflict reporting.
3. Finish the advanced editing and multi-file resolution UI on that stable model.

For the shortest path to both completed features, begin with the migration's
shared conflict/provenance model and avoid building a second UI-only conflict
format. Keep the [Translator handoff](../../5eApiTranslator/docs/aurora-translator-data-handoff.md) current
with each completed slice, changed rule, verification result, and deployment state.
