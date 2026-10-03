# Local correction and authoritative update policy

Agreed direction, 2026-09-13. The first implementation is in the working tree;
see [the implementation notes](local-correction-implementation.md) for its contract,
validation, and remaining UI/migration work.

Local content may be imported/cached in SQLite, but remains a distinct override
layer. A base record can receive an authoritative update without overwriting the
effective local correction seen by the builder.

## Persist intent with the local artifact

Store explicit correction intent in the existing XML file, and mirror it in SQLite.
The artifact is the durable record so a database rebuild does not lose protection;
no sidecar file is required. A separately namespaced `al:corrections` section under
the unnamespaced `elements` root passed the bounded compatibility checks in
[the metadata investigation](correction-metadata-compatibility.md). Version 1 now
has an implemented parser, evaluator, SQLite mirror, and retirement path.
Keep the namespace declaration on the metadata section, preserve the existing
`info` block, and store any original XML payload as escaped text rather than a live
subtree that the compatibility repair pass can mutate.

Metadata responsibilities:

- Stable correction identifier and local file/declaration locator.
- Operation: replace, add, remove, or rename, including original and replacement
  identities where appropriate. File-level provenance can identify the base file
  represented by a complete local copy.
- Target Aurora ID, with source-file provenance and original declaration fingerprint
  where needed to distinguish malformed inputs that reuse an ID. The accepted
  canonical key after content cleanup is Aurora ID alone; provenance identifies
  the declaration being repaired without becoming part of that canonical key.
- Original authoritative definition fingerprint and revision when known, with a
  versioned normalization scheme. A fingerprint detects change; it does not prove
  revision authority or semantic equivalence.
- Explicit correction flag, optional reason/upstream issue link, and review state.
- Related correction identifiers so a renamed element and repaired parent grant
  can be reviewed and retired together, avoiding dangling references.

## Update behavior

| Local declaration state | Incoming authoritative content | Action |
| --- | --- | --- |
| Unchanged companion copy, confirmed against its recorded baseline | Updated or removed upstream | Follow the authoritative base; do not pin incidental copied content. |
| Explicit correction | Upstream changes its target | Update stored base, preserve effective correction, surface a review diff. |
| Explicit correction | A matching file is found on disk, without verified download evidence | Mark incorporated / ready for review; keep protected. |
| Explicit correction | A fresh response from its configured authoritative upstream URL fully incorporates the correction and linked reference repairs | Automatically accept the published fix after validating and successfully importing that exact content. No manual approval is needed for this exact-match case. |
| Intentional local addition | No upstream counterpart | Preserve the addition. If upstream later uses its identity, request conflict review. |
| Legacy local edit without a trustworthy baseline or correction marker | Any overlapping change | Preserve it and request classification; missing metadata is not permission to overwrite. |
| Entire local file has no remaining marked corrections and fully matches authoritative content | Verified authoritative replacement is imported and available | Automatically retire the local override and its cached records; no further review is required. |

File retirement is separate from accepting a marked correction. After explicit
review or verified published-match acceptance clears the last correction, a fully redundant file can be retired
automatically. Confirm the whole file's effective content, including additions,
removals, renames, and linked reference repairs, against the authoritative base
without applying the overlay. No remaining markers alone is not sufficient.

### Verified published-match acceptance

Policy refinement, September 13, 2026: observed content fetched from the
authoritative upstream source can establish that the correction has been
published. Manual review is not required when its complete effect matches.
This supersedes the blanket rule that every incorporated correction must be
manually accepted. **The current implementation still requires explicit review;
download-evidence tracking and automatic acceptance are pending work.**

Bind the decision to the actual fetched payload's hash, source URL/provenance,
and the local correction revision being evaluated. A matching file on disk, a
URL embedded in XML, a timestamp, or a version string alone is insufficient.
An HTTP 304 alone is also insufficient unless a previously verified download
record already binds the unchanged upstream representation to those exact bytes.
Do not create a receipt by hashing an arbitrary local file after fetching ends.

Compare the full intended repair, including source attribution, rules, grants,
renamed targets, removal of the intended obsolete declaration, and all linked
operations. Preserve other legitimate declarations sharing a formerly malformed
ID. Accept a related group only when every operation is confirmed; partial or
differing repairs remain pinned for review. Unrelated changes elsewhere in a
file do not invalidate an exact match for a complete correction group, and do
not authorize deleting unique local content.

Stage the decision with the fetched content, validate/import it, and recheck
the local correction and incoming hashes before committing acceptance. Failed
or raced imports must leave protection in force. Persist accepted intent in XML
and mirror the evidence/state in SQLite so rebuilds retain the decision; no
sidecar is required. Recovery must handle an interrupted XML/database update
conservatively. Only then apply the existing whole-file retirement conditions.
Retire the file by renaming it with a `.retired-<unique-id>` suffix, so it is
recoverable and no longer matches the XML scanners. Remove its
override cache and invalidate affected runtime lookups only after the authoritative
replacement is available; preserve any still-unique local content.

Use a three-way comparison: original baseline, current local definition, and new
authoritative definition. Do not infer newness solely from a filesystem timestamp
or compare unrelated file-version sequences. Detect changes before regenerating
any synchronized full-file local artifact, preserving marked edits.

Renames/removals must suppress the intended obsolete base declaration even if an
unfixed upstream file is imported again. ID-only upsert is insufficient. Explicit
operation metadata replaces the need to permanently freeze an entire corrected
file while its unrelated elements evolve.

## Existing hotfixes and UI

The six full-file hotfix copies made earlier have verified original backups and
an ID-change manifest. They have now been explicitly annotated using those
backups: ten review-pending operations, with Devout/Tatsumi parent grants grouped
with their ID repairs. Other legacy local files remain unclassified. This does
not infer intent for or automatically rewrite unrelated installed local files.
See [the annotation record](hotfix-metadata-annotation-2026-09-13.md).

Advanced users should be able to inspect the three versions and related references
in the builder's compact conflict window, accept an upstream correction, keep or
edit their local correction, or open the larger authoring workflow in
Constellations/Aurora XMLHelper/Aurora Studio. Basic-mode sync must preserve marked
corrections too; hiding the advanced UI must not change data protection.
