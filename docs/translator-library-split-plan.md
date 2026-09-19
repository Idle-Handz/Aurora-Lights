# Shared content library: Translator split plan (draft)

Status: **draft, updated 2026-09-18.** Work happens on
`feature_shared-content-library` in both this repo and `5eApiTranslator`.
Nothing merges to `main` in either repo until Windows MAUI user-experience parity
is demonstrated.

## Decisions

1. **Builder.\* returns to legacy-only.** Logic added to `Builder.Data` after the
   2026-07-26 restoration moves out, except for the two small exceptions below
   (D1, D2). Builder.\* changes still pass the legacy parity gates
   (`docs/LEGACY_RESTORATION.md`).
2. **Aurora Legacy stays independent of Reflections.** Legacy gains no new
   package dependencies from this work.
3. **The app always builds its own content database.** The shared library owns all
   schema knowledge, reading and writing. No separate reader-compatibility
   contract is needed.
4. **The parity bar is the Windows MAUI user experience since the last commit.**
   Users don't see database versions, and nothing that previously worked may
   appear broken. Android and Mac improvements are optional upgrades.
5. **Content preferences live only in app settings.** Anything
   preference-related stored in the database is ignored and dropped, with no
   migration. Source restrictions come from settings (global default) and the
   character file (per character), and are applied at runtime.
6. **The multi-user engine is deferred.** The library must not add new
   static/global state.
7. **Feature branches in both repos.**

Resolved design questions:

- **D1:** keep a **self-contained** `ElementsFile.SaveContent` correction guard in
  `Builder.Data`, as a documented safety exception. Legacy's WPF fetch, bundle and
  source-update commands (4 call sites) and `Builder.Data`'s own
  `IndicesUpdateService` (3) rely on it to avoid overwriting corrected files. It
  checks for correction metadata directly, with no package dependency and no new
  public API.
- **D2:** replace the feature-specific `spell-*` parser cases with a generic rule:
  the parsers **preserve unrecognized attributes** in a new collection on
  `RuleBase` instead of discarding them. The existing "unable to parse" warning
  stays, since it is how typos get noticed. Reflections filters warnings for
  attributes it recognizes. `SpellAcquisitionResolver` reads from the new
  collection. **Follow-up:** add corrections/aliases for common attribute typos
  and mistakes.
- **D3:** mirror the Legacy source model, with a global default configuration
  plus per-character restrictions. The Settings-page "content packages" go away
  (see "Content sources and preferences").
- **D4:** distribution is a versioned NuGet package, pinned in one place, with an
  easy switch to try another version or a local build (see "Distribution").
  - *Revised 2026-09-19:* the mechanism is a **vendored local feed**, not GitHub
    Packages. GitHub's NuGet registry requires a token for every restore, even for
    public packages, and the repos have different owners. The user chose the
    easier of nuget.org and a vendored feed.
- **D5:** names are `Aurora.Content.Contracts`, `Aurora.Content` and
  `Aurora.DataIntegration`.
- **Open:** checkpoint commits on the feature branches (see the last section).

## Current state (verified 2026-09-18)

**One production writer, and no sync without it.** *(Corrected during Phase 2.
An earlier draft relied on the 2026-09-12 ownership audit, which `ec9dff8`
(2026-09-16) superseded.)*
- When the bundled exe is present, `ContentDatabaseService.SyncAsync` runs the
  Translator (`sqlite-import`) on the **primary** content root. The Translator's
  own `LocalCorrectionSync`/`ContentPreparation` handles corrections inside the exe.
- **Secondary roots are composed at read time.** `PreparedCatalogReader`'s
  runtime-file overlay handles them.
- Without the exe (Android, Mac), sync **fails** with "on-device import through
  the shared library is pending" and the existing database is kept. The exe
  lookup is `#if WINDOWS` only (`ContentDatabaseService.cs:56`).
- The copied `Aurora.Importer` writer (**data v10**,
  `AuroraDatabaseMetadata.cs:6`) and the Lights `LocalCorrectionSync.ImportAsync`
  are now called only by tests and tools.
- The app still uses Aurora.Importer's *read* side:
  - `OpenReadableConnection`, `IsStale`, metadata/health, and
    `LocalCorrectionSync.ReadStatuses`/`ReadRuntimeContent`
  - package list/toggle: `SetPackageEnabled` still writes `content_packages` and
    rebuilds caches until Phase 5 removes it

**The copied code has already diverged:**

| File | Lights (`Aurora.Importer` / `Builder.Data`) | Translator |
| --- | ---: | ---: |
| `AuroraSqliteImporter.cs` | 2,275 | 6,327 |
| `LocalCorrectionSync.cs` | 306 | 215 |
| `PreparedCatalogReader.cs` | 136 | 93 |
| `LocalCorrectionDocument.cs` | 324 | 324 (declared in `namespace Builder.Data.Files`) |
| `ContentAppendComposer.cs` / `ContentText.cs` | 73 / 74 | 73 / 74 |

**Lights-only reader/helper files** (candidates for the library):
`AuroraXmlCatalogReader` (587), `ContentDatabaseHealth` (76),
`RuntimeContentFiles` (91), `SpellcastingExtensionText` (56),
`TranslatorSpellcastingReader` (35), `AuroraDatabaseMetadata`, `AuroraImportProgress`.

**Schema knowledge spread across Lights** (raw-SQL call sites per file):
`DbElementLoader` 43, `CompendiumService` 12, `Aurora.PdfImport/CharacterInferenceEngine` 12,
`ContentDatabaseService` 1. The `Aurora.Importer` files are extra to these.
`DbElementLoader` alone reads about 25 tables directly.

**User state in the content database:** only `content_packages.is_enabled`.
Correction review decisions are authoritative in the local XML (`state`
attribute). The database tables `local_corrections`, `local_override_files` and
`local_correction_inputs` are mirrors that are deleted and re-inserted on every sync.

**Builder.\* changes since restoration:** `Builder.Core` has none (README only).
`Builder.Data` has exactly 8, and they account for all **265** API additions the
legacy gate permits:

| # | Change | API additions |
| --- | --- | ---: |
| 1 | `Content/Review/ContentReviewContracts.cs` (new) | 213 (#1 + #2) |
| 2 | `Content/Review/CanonicalContentAnalyzer.cs` (new) | ↑ |
| 3 | `Files/LocalCorrectionDocument.cs` (new) | 48 |
| 4 | `RequiredContentPolicy.cs` (new) | 3 |
| 5 | `ElementBase.ContentFilePath` property | 1 |
| 6 | `ElementsFile.SaveContent` guard against overwriting corrected files | 0 |
| 7 | `GrantRuleParser` / `SelectRuleParser` `spell-*` attribute cases | 0 |
| 8 | `SelectRuleParser`: removed "missing 'spellcasting' attribute" warning | 0 |

**Aurora Legacy is `Aurora.Lights/Aurora.Legacy.csproj`, and it references `Aurora.Logic`.**
Anything added to `Aurora.Logic`, including package references, is compiled into Legacy.

**Aurora.Logic's use of the correction contracts:**
- `DataManager.cs:257-317` applies local corrections while loading XML: runtime
  correction documents, origin paths, and suppressing obsolete originals.
- `ContentIndexUpdateService.cs:238` checks for correction metadata before
  overwriting a file.
- `SourcesManager` / `SourceItem` use only `RequiredContentPolicy.IsRequiredSource`.

**The Legacy source model already exists in shared code:**
- `AppSettingsStore.DefaultSourceRestrictions` (comma-separated source names) and
  `ApplyDefaultSourceRestrictionsOnNewCharacter`.
- `SourcesManager.LoadDefaults()`, applied in `CharacterManager.New`.
- Per-character restrictions stored in the character file.
- Reflections' Manage page already edits per-character restrictions, with
  **Load defaults** / **Save as default** buttons. It does not expose the
  apply-on-new-character toggle or a standalone default editor.

Restrictions **hide** a source's options but keep its content loaded. The
Settings "package" toggle instead **removes** content from the loaded projection.
That is the mechanism that left characters with 44–100 unset items in the
2026-09-15 rehearsal.

**Target frameworks:** every shared library already targets plain `net10.0`:
Builder.Core, Builder.Data, Aurora.Logic, Aurora.Documents, Aurora.Components,
Aurora.Importer, Aurora.PdfImport, Aurora.Web, Aurora.Tests, and the Translator.
Only UI projects are platform-specific: Legacy/Presentation are `net10.0-windows`
(WPF), and Aurora.App is `net10.0-windows10.0.19041.0` + `net10.0-android` (or
`-maccatalyst`) for MAUI. A plain `net10.0` library can be referenced by all of
them, so all new projects use `net10.0`.

**Inside the Translator (`AuroraTranslator.csproj`, one exe project, 19k lines):**
- The importer does **not** depend on the character-state engine (5,565 lines).
- The importer and the state engine both use the expression engine and
  `AuroraSelectionRules`.
- SRD creature import uses `SrdHelpers` and `SrdMonster`.
- `Microsoft.Data.SqlClient` is referenced but unused.
- `Program.cs` (3,211 lines) dispatches about 25 verbs through an `if` chain.

## Target architecture

| Project | Repo | Contents | Referenced by |
| --- | --- | --- | --- |
| **`Aurora.Content.Contracts`** (new, `net10.0`, no dependencies) | Translator | `LocalCorrectionDocument` + evaluation, `ContentReviewContracts`, `CanonicalContentAnalyzer` | `Aurora.Content`, `Aurora.DataIntegration` |
| **`Aurora.Content`** (new, `net10.0`; Contracts + `Microsoft.Data.Sqlite`) | Translator | See list below | Translator CLI, Aurora.App, Aurora.PdfImport, `Aurora.DataIntegration`, tests/tools |
| **AuroraTranslator CLI** (existing, slimmed) | Translator | Verb dispatch, baseline/regression commands, character-state engine, 5e-API models, `XellarantXmlGenerator` | — |
| **`Aurora.DataIntegration`** (new, `net10.0`) | Lights | See list below | Aurora.App, Aurora.Tests, tools — **not** Aurora.Legacy |
| `Aurora.Logic` | Lights | Gains small dependency-free pieces only; **no new package references** | (unchanged) |
| `Builder.Data` | Lights | Restoration baseline + the D1 guard + the D2 attribute collection | (unchanged) |
| `Aurora.Importer` | Lights | **Retired** | — |

`Aurora.Content` contains:
- preparation and correction workflow
- schema, import and writer, with multiple content roots in a single operation
- versioning
- the projection read API
- query APIs for Compendium, PDF inference and starting equipment
- health, metadata and change scan
- SRD creature import
- the expression engine and selection rules

The Translator CLI drops the unused `Microsoft.Data.SqlClient` reference.

`Aurora.DataIntegration` contains:
- prepared-catalog → `ElementBase` materialization (the `DbElementLoader` prepared path)
- XML fallback and the raw user overlay
- the parity service
- the correction-aware `DataManager` loading extension (below)
- `ElementProvenance`

**How Legacy avoids the contracts package.** `DataManager` gets a small optional
loading extension point, an interface in `Aurora.Logic`. Reflections registers a
correction-aware implementation from `Aurora.DataIntegration`. Legacy registers
nothing, so it loads XML exactly as original Aurora did. Consequence: Legacy
treats a corrected local file as an ordinary user override (original Aurora
behavior) instead of applying Reflections' correction rules. The D1 guard still
stops Legacy from overwriting that file. The metadata check in
`ContentIndexUpdateService` moves behind the same extension point, or relies on
the D1 guard.

**Aurora.App afterwards:**
- `ContentDatabaseService` calls `Aurora.Content` in-process, with no child
  process and no `#if WINDOWS` gate.
- `BundledTools/AuroraTranslator`, the untracked pin/restore scripts and
  `tools/publish-translator.ps1` are retired once parity is proven.

## Moving the post-restoration changes out of Builder.Data

| # | Destination | Notes |
| --- | --- | --- |
| 1–2 | `Aurora.Content.Contracts` | Namespace changes; update call sites. |
| 3 | `Aurora.Content.Contracts` | Diff against the Translator copy first. The line counts match, but content might not. |
| 4 | `Aurora.Logic` (`IsRequiredSource` only) | `IsRequiredPackage` is retired with packages. Locked infrastructure rows in the restriction editors stay locked in both apps. |
| 5 | `Aurora.DataIntegration`: `ElementProvenance` (a `ConditionalWeakTable<ElementBase, string>`) | Set through the `DataManager` extension point and by the loaders. Check that `ElementBaseCollection.GetFresh` copies keep their provenance. |
| 6 | Stays, rewritten as a self-contained check (D1) | Same behavior; no dependency on `LocalCorrectionDocument`. |
| 7 | Replaced by generic unrecognized-attribute preservation (D2) | Spell-specific cases removed. `SpellAcquisitionResolver` reads the preserved collection. |
| 8 | Restore in `Builder.Data` | If it's noise in Reflections, filter it with the existing `EngineLogNoiseFilter`. |

**Finish line:** `Compare-RestoredAssemblyApi.ps1` reports only the D2 collection
as an addition (down from 265). `Compare-BuilderDataBehavior.ps1` passes against
both builds. `Aurora.Legacy.csproj` builds with no new package references.

## Content sources and preferences

- The database stores **no** preferences. `content_packages.is_enabled` is
  ignored and then dropped, with no migration. Package kinds/ranks stop being a
  user-facing concept. The library may keep internal file/supplier provenance
  for corrections and diagnostics.
- **The whole catalog always loads.** Source restrictions are applied at runtime
  by `SourcesManager`, as in Legacy:
  - **global default**: `AppSettingsStore.DefaultSourceRestrictions` plus
    `ApplyDefaultSourceRestrictionsOnNewCharacter`
  - **per character**: restrictions stored in the character file
- **Settings:** remove the package list. Add a default-source-restrictions editor
  that reuses the same restriction tree as the Manage page, plus the "apply
  defaults to new characters" toggle, which Reflections doesn't expose today.
  Infrastructure sources (Internal/Core/Essentials) stay locked.
- **Restricting a source on an existing character disables its elements, not just
  its options** (user spec, 2026-09-18). When a source book is restricted for a
  character, either directly or via Load defaults:
  - Any **selected** elements from that source are deselected and unregistered,
    along with their nested selections and grants. Reuse the existing
    selection-owner cleanup and `BuildService` stale-selection validation.
  - The emptied choices appear in Build as **unresolved picks**, through the
    existing guidance (next-step banner, unresolved counts, row highlighting).
  - If the engine can't settle cleanly in memory, do a save → reload round-trip
    before re-snapshotting.
  - Implement this in Reflections' layer, not in shared `SourcesManager`, so
    Legacy's behavior is unchanged.
  - Known gap to handle: `ValidateSelections` currently *keeps* a selection when
    its valid-option set is empty (`validIds.Count == 0`). A choice whose options
    all come from restricted sources must still be cleared.
  - Open edge case: a character saved (e.g. by Legacy) with selections from
    sources it restricts. Decide whether to surface these on load or only when
    restrictions change.
- **Compendium:** decide whether it applies the global default restrictions as a
  filter. Verify what Legacy did and match it.
- **Accepted user-visible changes:**
  - package toggles are replaced by source restrictions
  - previously disabled packages come back as loaded content (hidden only if
    restricted)
  - characters no longer lose items because their content was switched off

## Database lifecycle in the app

- The database is a disposable artifact. On a library schema/data version
  mismatch, the app rebuilds it.
- Phase 1 proved the library writes exactly what today's exe writes, so existing
  Windows users' v12 databases can be accepted as-is, with no rebuild on first
  launch. Secondary roots keep being composed at read time; the library takes
  multiple roots so this can later move into import. Android/Mac gain on-device
  import as an optional upgrade. A fresh v12 build took 23–25 s in rehearsal.
- The v10/v11 reader paths in `DbElementLoader` are removed after parity.
- `CompendiumService` and `CharacterInferenceEngine` raw SQL move behind library
  query APIs.

## Distribution (vendored local feed, implemented in Phase 3)

- The Translator's `Aurora.Content.props` holds the package version and metadata.
  Packages record the source repository and commit.
- `tools/update-content-library.ps1` (Lights):
  - packs both projects from the sibling checkout (it refuses an uncommitted tree
    unless `-AllowDirty`)
  - copies them into `vendor/nuget/` (committed)
  - appends provenance (commit, SHA-256 per package) to `vendor/nuget/manifest.json`
  - updates the pin
- **Vendored versions are immutable.** The script refuses an existing version,
  because NuGet caches packages by version. Bump the version for every change.
- **One pin:** `AuroraContentVersion` in `AuroraContent.props` at the Lights root,
  referenced as an exact version (`[x.y.z]`).
  - Only consuming projects import this file; **Aurora.Legacy never does**.
  - `nuget.config` adds `vendor/nuget` as a source. `.gitignore` re-includes
    `vendor/nuget/*.nupkg`.
- **Local switch:** build with `-p:UseLocalContentLibrary=true` to use a
  `ProjectReference` to `../5eApiTranslator` instead (override the location with
  `AuroraContentSource`).
- CI needs no secrets or extra steps; the packages are in the repo.

## Parity gates (Windows MAUI)

Capture the baseline from the current working tree before changing anything (Phase 0).

1. **Database:** library-built database vs today's exe-built database, row by row
   across all application tables (reuse the 61-table comparison from
   `content-priority-impact-audit-2026-09-13.md`). Build timestamps and absolute
   paths are excluded.
2. **Projection:** identical prepared projections with all content loaded.
3. **Characters:** `ContentDatabaseRehearsal` runs for Test E, Art E, Fresh E,
   Testy, Gobric, Remy Morningstar and the prepared-paladin fixture. First-load and
   round-trip results must match the baseline, including the known diagnostics
   (Art E Claw, Fresh E).
4. **Service failure paths:** the 13 fixture checks from
   `pre-ui-validation-2026-09-15.md`. Disable/re-enable becomes
   restriction-based.
5. **Legacy:** Builder.\* API and behavior gates; `Aurora.Legacy.csproj` builds
   with an unchanged package graph.
6. **Suites:** Aurora.Tests (currently 548 pass / 1 skip) and AuroraTranslator.Tests.
7. **Manual UI:**
   - the 7-step checklist in `pre-ui-validation-2026-09-15.md`
   - Settings default restrictions and the new-character toggle
   - the Manage page's per-character restrictions and Load/Save defaults
   - Compendium
   - PDF import
   - first launch after upgrade

## Phases

0. **Baseline.** Checkpoint both working trees, including the Translator's 15+
   uncommitted files. Capture gate 1–4 outputs from the current code.
1. **Extract inside the Translator repo.** Create the Contracts and
   `Aurora.Content` projects and `git mv` files into them. The CLI references the
   library; drop SqlClient. This phase is **behavior-neutral**: CLI output is
   byte/row-identical and Translator tests pass. The parent-inference/rank issue
   is carried over unchanged and fixed afterwards with its own tests.
   **Approach:**
   - New projects at the Translator repo root: `Aurora.Content.Contracts/` and
     `Aurora.Content/`, both `net10.0`, added to `AuroraTranslator.sln`.
   - `git mv` the importer partials, preparation/correction/prepared-content
     files, `AuroraDataIntegrity`, `AuroraSelectionRules`,
     `AuroraSpellcastingXml`, the expression engine, `SrdHelpers` and the Aurora/SRD
     models into them.
   - `LocalCorrectionDocument` goes to Contracts.
   - The CLI keeps `Program.cs`, the character-state engine (+ its
     `AuroraRuntimeSelectionRules` partial), `XellarantXmlGenerator` and `Data/`.
     The 5e-API models (`Spell`, `SpellDC`, `DamageComposite`, `BaseApiClass`)
     moved to the library after all, because the importer's `AuroraSpell`
     inherits `Spell`.
   - Nearly all moved types are `internal`. To stay behavior-neutral, the library
     grants `InternalsVisibleTo` to the CLI and tests, and **namespaces are left
     unchanged** in this phase. The public API and namespace alignment come with
     Lights integration (Phase 4).
   - Verification: build `fresh-*` databases with the new CLI through a copy of
     the rehearsal harness whose `BundledTools` holds the new build. Compare them
     to the baseline with `compare_databases.py --ignore "*.created_utc" --ignore
     database_metadata.built_utc`; they must be identical. Translator tests must
     stay 65/65.
   **Result (2026-09-18, Translator `25167f3`): complete.**
   - The committed pre-move source (`f1f76e1`) first proved to reproduce the
     bundled Translator exactly: 68 tables identical, projections identical.
     So the comparison isolates the move itself.
   - The post-move build's fresh databases (both builds) are **identical to the
     baseline in all 68 tables**, ignoring timestamps.
   - Loaded projections are identical (101,333 / 94,005 elements).
   - The refresh, XML-parity, fallback and failure-path checks succeed with
     identical warning text. Translator tests pass 65/65.
   - Three hidden dependencies surfaced at compile time and were resolved without
     behavior change:
     - `CharacterWarningResult` moved beside the expression context that stores it.
     - `AuroraSpell` inherits the 5e-API `Spell`, so those models moved too.
     - `PreparedContentWriter` now calls `ContentText.SplitTopLevel` directly
       instead of the CLI's one-line pass-through.
   - Characters were not re-run: app code is unchanged and its database input is
     row-identical. They will be re-run when the app changes (Phase 4).
2. **Reconcile duplicates.** Diff every Lights/Translator pair and merge
   Lights-only fixes into the library (the `LocalCorrectionSync` 306-vs-215 gap
   matters most).

   **Result (2026-09-19): complete.**

   Pairs that already matched:
   - `LocalCorrectionDocument`: byte-identical.
   - `ContentAppendComposer`, `ContentText`: identical apart from the namespace line.

   The two that differed were decided by the user:
   - **`PreparedCatalogReader`:** the library adopts the Lights copy verbatim. It
     is a superset with the runtime-file overlay that the app uses for secondary
     roots and unsynced edits. Its unresolved-append rule checks the live
     catalog instead of trusting the stored status; on all 1,215 operations in
     both real databases, stored status and catalog membership agree.
   - **`LocalCorrectionSync`, import side:** the Translator's version stays. It is
     today's production behavior, running inside the exe, and it enforces every
     Lights-copy safety rule plus symlink, duplicate-ID, spelling and append-conflict
     checks. The Lights `ImportAsync` is test/tool-only and retires in Phase 7.
   - **`LocalCorrectionSync`, read side** (`IsStale`, `ReadStatuses`,
     `ReadRuntimeContent`): these now open read-only, writable only to recover a
     leftover rollback journal. They use a new public
     `ContentDatabase.OpenReadableConnection`, identical to the Lights helper the
     app already uses. Import-side opens are unchanged.

   Verified with `verify_translator_build.sh`: both fresh databases are identical
   to the baseline, both projections are identical, every rehearsal check matches
   the baseline's warnings, and Translator tests pass 65/65.
3. **Distribution** (D4).

   **Result (2026-09-19): complete.** `Aurora.Content` / `Aurora.Content.Contracts`
   0.1.0 are vendored from Translator `1423aa8`:
   - Adding package metadata changes only assembly metadata; `verify_translator_build.sh`
     still matches the baseline exactly.
   - A throwaway consumer importing `AuroraContent.props` restored from the vendored
     feed and read the baseline database: prepared, 20,876 elements, 21 unresolved
     appends.
   - The same consumer resolved to a `ProjectReference` with
     `UseLocalContentLibrary=true`, with identical output.
   - Re-vendoring an existing version is refused.

   **Phase 4 prerequisites found here:**
   - **SQLite version alignment.** `Aurora.App`, `Aurora.PdfImport` and
     `Aurora.Importer` reference the floating `Microsoft.Data.Sqlite 9.*`
     (currently 9.0.20). The library needs `10.0.12`, which is what the importer
     runs in production. Referencing both would fail restore with NU1605. Pin
     consumers to `10.0.12`; this also removes a non-reproducible floating version.
     Aurora.Legacy doesn't reference SQLite.
   - **Duplicate type names.** Aurora.Importer's `PreparedContent` files declare
     `namespace AuroraTranslator.Content`, the same full names as the library
     (`PreparedCatalogReader`, `ContentAppendComposer`, `ContentText`…).
     `LocalCorrectionDocument` exists in both `Builder.Data` and Contracts as
     `Builder.Data.Files.LocalCorrectionDocument`. A project seeing both gets CS0433.
     Phase 4 must remove Aurora.Importer's copies in the same step it adds the
     library, and must either give Contracts its own namespace or coordinate the
     Builder.Data removal (Phase 6).
   - **Public API.** The library's `LocalCorrectionSync` is `internal`; the app needs
     `ReadStatuses`, `ReadRuntimeContent`, `IsStale`, metadata/health and the
     runtime-file builder as public API.
4. **Lights consumes the library.** Add `Aurora.DataIntegration` and the
   `DataManager` extension point; `ContentDatabaseService` goes in-process;
   multiple roots in one operation; health/metadata come from the library.

   **Step 1 (2026-09-19): in-process import, complete.** Aurora.Content 0.2.0
   (Translator `dc3cf01`) is vendored. The user approved all three prerequisites:
   the SQLite 10.0.12 pin, the `Aurora.Content.*` namespaces, and real refresh
   progress.
   - `Aurora.DataIntegration` (`net10.0`) holds the content database services
     that moved out of Aurora.App with `git mv`: `DbElementLoader`,
     `ContentDatabaseService`, the parity/fallback/overlay services,
     `ContentDirectoryResolver`, `DebugLogService`, `StartingEquipmentParser` and
     `ElementOption`. The app, Aurora.Tests and the rehearsal harness now share one
     compiled copy instead of linked sources. Aurora.Legacy doesn't reference it,
     and its package graph is unchanged.
   - `ContentDatabaseService.SyncAsync` calls `ContentImport.ImportAsync` in
     process. The bundled exe, its compatibility probe and the `#if WINDOWS` gate
     are gone.
     - Library phases map onto the existing Settings bar: preparing/reading
       0–50%, comparing/writing 50–90%, resolving/activating 90%.
       `ContentImportProgressMappingTests` covers the mapping.
     - **User-visible upgrade:** refresh now works wherever the app runs:
       Android, Mac, and CI-built Windows releases. Those never had the bundled
       exe, so refresh used to fail there with "importer required". Android
       still needs an on-device check.
   - `Microsoft.Data.Sqlite` is pinned at 10.0.12 in Aurora.App,
     Aurora.Importer and Aurora.PdfImport (was a floating `9.*`).
   - `verify_content_library.sh` replaces `verify_translator_build.sh`. It builds
     the harness against a Translator checkout, or against the pinned package
     with `AURORA_CONTENT_SOURCE=pinned`, then runs the suite and
     `compare_to_baseline.py`.
   - **Parity:**
     - The full suite with the in-process refresh matches the Phase 0 baseline:
       both fresh databases row-identical, both projections identical, all 5
       service checks with identical warnings, 60/60 characters.
     - The pinned 0.2.0 package's database suite matches too.
     - Aurora.Tests 562 pass / 1 skip. Windows and Android app builds are clean.
   - The `BundledTools` copy item in Aurora.App is now dead. It's removed with the
     other exe tooling in Phase 7.

   **Deferred from this phase: multiple roots in one import.** The Translator's
   catalog builder reads one root, and secondary roots keep being composed at
   read time (as before). A multi-root import needs its own precedence and
   correction rules and its own parity check.
5. **Sources and preferences.** Remove package toggles; add the Settings default
   restrictions editor and new-character toggle; load the full catalog; drop DB
   preference reads.
6. **Builder.Data cleanup** (the 8 items) and the `Aurora.Logic` additions; legacy gates.
7. **Retire the old paths:**
   - `Aurora.Importer` and the copied writer
   - the v10/v11 reader paths
   - the bundled exe, the pin scripts and `publish-translator.ps1`

   Also point `tools/RunImporter` and the rehearsal harness at the library.
8. **Full parity run,** then merge to `main`.

Later / optional:
- **automatic correction retirement on verified origin downloads** (user
  requirement, 2026-09-19): when a source freshly fetched from its update origin
  fully matches a correction file's intended result, accept those corrections and
  retire the file. The `ContentDownloadEvidence` contract exists, but no
  downloader evidence producer does yet. Matching files already on disk must
  never count as acceptance (`docs/local-correction-policy.md`).
- in-process import on Android and Mac (validate native SQLite packaging)
- attribute typo corrections/aliases (D2 follow-up)
- the parent-inference fix
- the 462 generated-ID collision groups (scroll generator)
- the multi-user engine

## Phase 0 baseline (2026-09-18, complete)

Location: `buildtmp/parity-baseline-20260918-190022-f89a8d` (ignored). It was
created by `tools/ContentDatabaseRehearsal/prepare_baseline.py` and run with
`run_parity_suite.sh`. Installed content is read-only: 1,189 XML files, the v12
database, and 60 characters (59 of the user's plus the prepared-paladin
fixture). All 65,083 absolute paths in the database copy were relocated into the
case, and no live references remain. The relocated copy scans as up to date.

| Check | Result |
| --- | --- |
| Fresh build with the bundled Translator (×2) | Both succeeded, 24–25 s |
| **Determinism** (fresh-a vs fresh-b, `compare_databases.py`) | **All 68 tables identical**, row IDs included, after ignoring `*.created_utc` and `database_metadata.built_utc`. So gate 1 can be strict row-for-row equality. |
| XML ↔ database definition parity (fresh) | Pass |
| Fallback checks (9) / service failure checks | Pass / pass |
| AuroraTranslator.Tests | 65/65 pass |
| Aurora.Tests (same code, 2026-09-17) | 548 pass, 1 skipped |
| Character load/save/reopen, one process each (60) | 19 fully clean; 41 partial first load; 9 round-trip issues (see below); 0 timeouts; ~39 s per character |
| Installed inputs after the run | Unchanged (1,249 files re-hashed) |

**Finding: saved package toggles hide content *and* its modifications.** The
installed database has **Ryoko's Guide to the Yokai Realms** disabled (plus the
stale `ALE.xml` flag, which the required-content policy already overrides).
Compared with a fresh build (everything enabled), the installed projection loads
94,005 runtime elements instead of 101,333:
- 7,326 IDs are missing: about 865 Ryoko definitions plus 6,461 elements
  generated from them.
- **118 elements from other books differ in content** because Ryoko's append
  operations are excluded:
  - 102 PHB spells (the Bender and Spirit Caller spell lists)
  - 14 proficiency groups (Ryoko weapons such as *Claw* added to Simple Melee
    Weapons and similar)
  - 1 class and 1 multiclass entry

Under the "whole catalog loads, restrictions apply at runtime" model, a source's
appends modify the shared catalog for every character, even when that character
restricts the source. This is also why v12's Ryoko exclusion caused the earlier
Claw/proficiency warnings on characters saved under v11. Open question for the
user: see the next section.

## Decided: restricted sources' effects on other books (2026-09-18)

- **Option B, restriction-aware grants:** skip grants that would give a character
  an element from a source that character restricts (e.g. no *Claw* proficiency
  when Ryoko is restricted). This is implemented through a Reflections-side
  extension point in grant processing, so Legacy's behavior is unchanged.
  Tag-only appends (spell-list entries) need no filtering.
- **Symmetric re-enable:** un-restricting the source re-evaluates grants, so the
  suppressed grants are applied again.
  - Interpretation to confirm: *selections* that were cleared stay as re-pick
    prompts. Only automatic grants come back automatically.
- **Load validation:** grants suppressed by a character's own restrictions are
  expected. `CharacterLoadValidation` must report them as informational, not as
  missing saved elements.
- **One-time seeding of global defaults:** on first launch after upgrade, read
  the old database's disabled packages once and add the sources of their
  elements to `DefaultSourceRestrictions`. Required infrastructure sources (e.g.
  `ALE.xml`) are excluded, since they can't be restricted. This touches app
  settings only, never character files. After this one read, database
  preferences are ignored.

## Pre-existing issues surfaced by the baseline (not caused by this work)

These are recorded so parity comparisons treat them as the baseline, not as
regressions. Counts are across all 60 characters.
- **A renamed content ID (24 characters):** characters saved with
  `ID_WOTC_DMG_PROFICIENCY_WEAPON_FUTURISTIC_FIREARMS_LASTER_PISTOL` (typo) now
  miss it, because content has `..._LASER_PISTOL`. They warn on every load.
  Renaissance musket/pistol proficiencies (11 and 10 characters) exist in the
  catalog but are no longer restored; this is likely a changed grant path and
  needs its own trace. The D2 follow-up (aliases for typos/renames) should map
  old saved IDs to their replacements.
- **Ryoko-derived grants** (*Claw* 5, *Tessen* 3, Ryoko options 3) are missing
  because Ryoko is disabled. Resolved by Option B plus the seeded defaults.
- **A spurious ASI warning on reopen (7 characters):** *Aurora*, *Daiyu Ao-shi*,
  *Gobta*, *Luna*, *Lusten Winterblush*, *Michelle Character 1* and *Remy
  Morningstar* keep identical state, choices and inventory, but reopening a saved
  copy reports a missing ASI option. This is the same family as the Art E
  stale-racial-ASI issue.
- **Real round-trip changes (2 characters):** *The Doc* **loses a multiclass
  level** (`ID_INTERNAL_MULTICLASS_LEVEL_9`) and gains
  `ID_RDDT_AA_CLASS_FEATURE_GUNSLINGER_BULLET_TIME`. *Remy Morningstar (Strahd)*
  gains `ID_TBOX_COMPANION_CHICKEN`. Both are pre-existing data-integrity bugs, to
  be investigated separately from this project.

## Checkpoint commits (approved 2026-09-18)

Checkpoint commits go on the feature branches, never `main`. The merge to `main`
lands as one commit per phase.
- Baselines: Translator `f1f76e1` (pre-existing uncommitted work); Lights
  `6c1660a` (exception logging), `86459a5` (docs), `e812560` (pre-existing pin
  scripts).
- The pinned Translator zip is left untracked because it's a build artifact.
- Before pushing the Translator branch: its new grant-repair test fixtures
  quote description text from DMs Guild / third-party products. The tests likely
  only need IDs and grants, so consider stripping the prose if the repo is public.

## Risks

- `LocalCorrectionSync` divergence: the Lights copy carries app-lifecycle
  behavior (retirement, recovery). Reconcile it under the 25 correction-lifecycle
  tests and the failure-path checks.
- `DbElementLoader` / `XmlContentFallbackService` may touch MAUI APIs. The
  rehearsal harness already compiles them outside MAUI, which suggests they're
  movable, but this needs checking.
- The `DataManager` extension point changes Legacy's handling of corrected files
  back to original Aurora behavior. Confirm nothing in Legacy's own tests relied
  on correction-aware loading.
- The Translator's uncommitted work predates this plan. Phase 0 must capture it
  before any `git mv`.
