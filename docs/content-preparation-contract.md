# Content preparation and review contract

September 13, 2026. First typed foundation implemented in
`Builder.Data/Content/Review/`; not wired into production import or the UI yet.

## Responsibility boundary

The **content-preparation layer** resolves the proposed content set before the
SQLite writer receives it. It can run inside the builder, or headlessly for the
Translator CLI. Sharing this logic does not require putting conflict resolution
inside the SQLite importer or inside a UI component.

```text
Fetched/installed XML + local corrections + publication evidence
                              |
                  Shared content preparation
              classify, compare, retain provenance
                              |
       policy-resolved cases / app or authoring-tool review
                              |
                Finalized content + provenance
                              |
             SQLite writer: validate, store, link
                              |
              Validated candidate activation
```

- Preparation owns duplicate classification, correction evaluation, authority
  evidence, and conflict/review results. The app provides interaction when needed.
- XMLHelper/other authoring tools can supply diagnostics and proposed edits.
  Preparation binds those suggestions to actual file revisions and validates the
  approved result, including related references.
- The writer accepts the finalized canonical/effective input plus provenance and
  override tracking required for persistence. It checks invariants and fails if
  invalid/unresolved input reaches it; it does not select a winner, decide user
  intent, display a dialog, or invent an ID repair.
- Activation and correction-acceptance/retirement coordination remain workflow
  responsibilities. A successfully constructed proposal or download-evidence
  record does not authorize a write or prove publication.
- A headless CLI should call preparation too, applying agreed deterministic rules
  and reporting unresolved cases. It must not fall back to arbitrary ranking just
  because it has no review UI.

Current `LocalCorrectionSync` resides in `Aurora.Importer` but acts as a workflow
wrapper. This quick foundation does not move or redesign that existing runtime
pipeline. Final extraction can package preparation and writing together while
keeping their APIs/responsibilities separate.

## Shared records

| Concept | Concrete representation / rule |
| --- | --- |
| File provenance | `ContentFileKey`: stable configured `RootId` plus relative file path. Root ID is not the absolute path, current root index, Source label, or package display name. |
| Exact input revision | `ContentFileVersion`: file key plus SHA-256 of captured raw bytes. Snapshot loading clones bytes so parsing and hash describe the same input. |
| Declaration occurrence | `ContentDeclarationKey`: file version plus zero-based ordinal among direct `element` children. Distinguishes two identical declarations in one file; not durable across file revisions or character saves. |
| Definition snapshot | Aurora ID, type, name, source label, complete element XML, current conservative structural fingerprint, and declaration key. Source remains attribution rather than canonical identity. |
| File snapshot | Parsed full XML, layer, ignore state, claimed update URL and definition snapshots. Claimed URLs are not verified download evidence. |
| Repeated identical content | All supplying declaration keys, not one discarded duplicate row. The eventual writer receives canonical content plus all retained provenance. |
| Review case | Category, deterministic evidence-set case ID, exact IDs/definitions, and reference-impact inspection state. New file bytes change the case ID. |
| Reference impact | Typed grant/append/selection/requirement/support/other locations, owning declaration where known, and `NotInspected`, `Partial`, or `Complete`. An empty list is not proof that no references exist. |
| Correction review | Baseline, local and incoming file snapshots; existing XML `LocalCorrection` operations; related file/correction keys; optional download evidence; reference impact. |
| Download evidence | Payload file version, requested and response URLs, fetch time, optional repository revision and ETag. Trusted downloader/authority checks must produce and validate it. A DTO alone proves nothing. |
| Resolution proposal | Workflow intent, case IDs, expected input revisions, related correction keys and evidence. It is not an executable patch or acceptance authorization. |
| Authoring finding | Original provider diagnostic JSON bound to the inspected file revision and, when unambiguous, a declaration key. Preserve provider-specific repair details. |

These are in-process C# records with contract version 1, not a finalized JSON
transport/schema migration. Persistent root registration, serialization adapters,
trusted evidence production, reference scanning and resolution execution remain
work. There is no new sidecar and no new database table in this slice.

## Read-only classification now available

`CanonicalContentAnalyzer.Analyze` accepts one revision per authoritative file.
Local overlays are explicitly separate inputs to the correction workflow.

- Same exact Aurora ID and same complete structural fingerprint: retain every
  occurrence as an identical-declaration set. Child/text differences remain
  conservative differences; attribute order is ignored by the existing algorithm.
- Same ID with differing definitions: report the complete candidates, without
  choosing one. A different source label is still a difference to investigate.
- Different IDs with matching display text/mechanics: do not collapse them.
- Case or surrounding-whitespace variants: report identity spelling for review,
  preserving the original IDs. This flags disagreement among existing consumers;
  it does not settle the pending canonical normalization policy.
- Ignored files do not supply active declarations. Invalid roots, DTDs and missing
  IDs fail snapshot construction rather than becoming an apparently valid catalog.
- No reference scan is performed yet: cases explicitly report `NotInspected`.

Classification does not mutate XML/SQLite or activate content. The existing
production importer and name/description option collapse are unchanged.

## AuroraXMLHelper reuse findings

Inspected current sibling checkout:
`C:\Users\Ralla\source\repos\AuroraXMLHelper`. No changes were made there.

`src/aurora-xml-patterns.js` already emits diagnostics with `severity`, `category`,
`fileName`, `elementId`, `elementName`, `elementType`, `message`, `suggestion`,
`node`, and `repairs`. Repair records include `kind`, `confidence`, description,
target, replacement/current values or placeholders, and XML sample/snippet data.
Targets can include line and node XML. Preserve these records through an adapter
rather than creating another authoring-rule vocabulary in the builder.

Existing repair kinds include `set-element-attribute`, `set-rule-attribute`,
`replace-supports-text`, `insert-setter`, `wrap-document-root`,
`edit-comment-text`, and `regenerate-element-id`. Confidence and sample XML are
suggestions, not permission to apply an edit. File names, lines, and a duplicated
ID do not uniquely locate a safe edit: bind against full file version and
declaration occurrence before writing, and reject stale/ambiguous targets.

`src/app.js` supplies `analyzeRepairXmlText` and `renderRepairPreviewHtml`, already
showing flagged nodes, suggested samples and relevant authoring context.
`tests/parser-regression.test.js` contains a read-only repair-preview regression.
This is useful UI/diagnostic work to reuse; it is not yet the builder's complete
three-way correction/review/retirement workflow.

One deliberate adaptation is required: XMLHelper currently flags every repeated
ID and proposes manual `regenerate-element-id` repairs for later occurrences.
It also flags byte/structurally identical repeated definitions. Our preparation
policy silently consolidates harmless identical same-ID declarations, retaining
their suppliers. Do not automatically apply placeholder suffixes such as `_2`,
or let that generic authoring suggestion supersede the reviewed content identity
and reference policy.

The implementations are JavaScript and C#, respectively. Reuse can start with
provider JSON adapters and common fixtures; the C# model does not claim to execute
the JavaScript diagnostics or embed the XMLHelper UI. No replacement suggestion
engine was added in this work.

## Verification and next small slice

- 16 focused `ContentReviewContractTests` passed: same-file/multi-file provenance,
  different grants/source labels, distinct IDs, root identity, stale evidence IDs,
  spelling variants, metadata isolation, ignored files and malformed input.
- Two read-only probes of XMLHelper's actual analyzer passed: the archived
  Adamantine/Walloping collision emits a manual ID-regeneration suggestion, and
  identical duplicates are also reported. The original input was unchanged.
  This was not a full XMLHelper test-suite or browser run.
- No production import, schema migration, live UI change, app deployment or
  publication was performed.

Next: adapt actual preparation results and XMLHelper diagnostics into this model,
add authoritative download evidence and reference inspection, then expose a
read-only review view. The transactional canonical migration and resolution
executor remain larger implementation steps. Keep
[the Translator handoff](../../5eApiTranslator/docs/aurora-translator-data-handoff.md) current as these land.
