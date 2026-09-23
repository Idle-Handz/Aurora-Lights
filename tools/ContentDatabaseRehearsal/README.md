# Content database rehearsal

Windows-only headless integration tool. It compiles the actual Lights database,
projection, fallback, and parity service sources and uses the bundled Translator.
It does not launch MAUI, render pickers, or exercise character UI interactions.
The only copied UI type is the shape-only ElementOption DTO.

prepare.py captures SHA-256 hashes of installed XML/database, creates a consistent
read-only SQLite backup, checks the existing alias drafts against their recorded
input hashes, and copies the corpus into isolated cases under buildtmp.
Only the v12 cases receive the three drafts. Installed character files are never
included or modified. The paths reflect this project's current local content setup.

Build with dotnet build tools/ContentDatabaseRehearsal/ContentDatabaseRehearsal.csproj.
Run the resulting executable with scan, refresh, load, snapshot, parity,
legacy-xml-load, reload-check, failure-check or dump, followed by the disposable
case directory. A case must contain
.aurora-rehearsal, and its XML/database live in its custom subdirectory.
An optional third argument names a secondary XML directory. Each run writes its
JSON result and application log inside the case. Save earlier results before
repeating a mode.

legacy-xml-load loads content the way Aurora Legacy does, through DataManager
XML with no database, and opens each character to show what Legacy resolves.
parity compares complete parsed definition XML, not just names or counts.
audit.py <rehearsal-root> checks SQL integrity, identity counts, fresh/migrated
prepared definitions and append operations, and hashes the installed inputs again.

Timings are observational and include service work, not GUI startup. Peak working
set is for the rehearsal process; refresh now imports in process, so it is included. Load measurements include runtime postprocessing and caches.
Result/log files are retained under the ignored buildtmp directory for review.

profile-load measures managed memory after collection, after invalidating the
fallback index, and after dropping its retained publication callback. It destroys
only that disposable process's cache; it is not a usable interactive session.
The blocking, non-inlined load wrapper avoids retaining completed async frames in
the measurement caller. profile-projection independently measures the retained
cost of creating the fallback index while keeping its input projection alive.
Forced collection is diagnostic only, not a proposed production optimization.

fallback-check runs nine behavioral checks against the linked application service:
list/equipment lookup, snapshot staging/activation/rollback, mutation isolation,
materialization provenance and exclusion of filtered definitions.

Following the authorized September 15 archive, use prepare.py --exclude-draft
efa-class.xml to avoid reintroducing the retired Artificer variant. The exclusion
is explicit and recorded in the new manifest. Historical rehearsal artifacts and
their three-draft manifests are preserved. The original audit's unchanged-input
expectation predates the archive; installed-archive-audit.json verifies that only
that expected file moved, its bytes match, and the production database is unchanged.

characters runs shared character load/save/reopen checks using the actual app
selection/spell handlers from MauiContextStubs.cs. Its third argument, when present,
is a filename in the case's characters directory, not a secondary content root.
Use a fresh process per character when diagnosing lifecycle problems. Each load
has a 120-second diagnostic timeout; a timeout stops the batch because the shared
singleton cannot safely serve another character alongside that task. Output goes
to roundtrip; original copies and real user files are not saved. Preserve per-file
results before another run overwrites characters-result.json. Prepared states,
registered IDs, choice metadata and inventory are compared. Volatile runtime GUIDs
are normalized in choice keys; stable identity components are retained. This
does not exercise the app's BuildService extras/normalization or its UI wrapper.

reload-check measures four full-corpus reloads, then builds an independent tiny
catalog to check secondary XML changes, supplier toggles, failed reconstruction,
rejected refresh and cancellation through the app services. failure-check runs
only those fixture cases, avoiding another expensive corpus reload after a harness
fixture repair. Malformed local XML and disabled supplier state are created only
inside the disposable case. Results and memory observations are in separate files.

Pre-UI findings, content terminology and the Release package checks are recorded in
docs/pre-ui-validation-2026-09-15.md. Local files are not inherently overrides;
the harness's historical alias drafts do not establish desired authoring intent.

`guard-correction` reads the installed PHB 2024 local pack file and its authoritative
baseline, writes an annotated candidate only into the disposable case, and checks
that all pre-existing XML content is preserved and the Guard crossbow reference is
protected. It never installs the candidate or changes the production database.

`farmer-annotation` similarly stages the intentional local Farmer preference as a
linked replacement plus added Tough feature. It verifies unchanged local content,
the canonical Farmer ID, both feature grants, complete protection metadata, and
survival of an unreviewed upstream change. It writes only a candidate; installation
into the user's content directory is a separate, explicitly authorized operation.
Character runs additionally write bounded lifecycle logs and choice diagnostics
(IDs and registration state only) to help distinguish inactive choices from
missing definitions. These logs do not include portrait data.

`characters-after-reload` exercises an in-session catalog replacement. Supply a
disposable `previous-v11.sqlite` beside the case marker; the normal database in
`custom` must be the current v12 copy. It loads Test E (or the optional third-argument
filename) against v11, restores v12 in a `finally` block, then runs the character
checks without restarting the process. The input character files remain untouched.
`before-reload.json` records the outgoing state. Character checks now fail on any
missing-grant diagnostic even if `CharacterFile.Load` eventually reports success;
the headless harness does not display the corresponding native modal dialogs.
`proficiency-projection.json` records proficiency IDs, suppliers, and grant targets.

`asi-check` uses a disposable copy of the previously failing Art E sample. It
checks load/save/reopen integrity, all six additional ability scores, retention
of the background ASI authority/combination, removal of the inactive racial
subtree, idempotence, legal racial choices without the background, and cleanup
when that background is restored. It edits only in-memory state and disposable
roundtrip files. The real character and installed content remain unchanged.
