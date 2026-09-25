# Shared content library: Translator split plan

Status: **verified and merged, updated 2026-09-24**.
Phases 4-7 are complete, the supplied handoff records the manual UI pass as cleared,
and the committed-source 0.7.1 uptake passed the verification below. Translator is
merged to `master`; Lights was fast-forwarded to `main` through `6b2f7e8`,
preserving all 45 feature-branch commits as approved by the user. This records
source integration, not a tagged application release.

**September 24 policy follow-up:** local package 0.7.0 (schema 1/data 13) implements
first-install conflict exclusions and blocking invalid corrections. See the
[policy and verification record](content-conflict-policy-2026-09-24.md). Selective
preservation remains deferred; the current scan does not support a greater-than-15%
per-refresh conflict estimate.

**September 23 review follow-up:** the local 0.6.1 fixes, regression evidence,
remaining skip-policy decisions, and manual checks are recorded in
[the fix record](content-library-fixes-2026-09-23.md). This supersedes the earlier
0.6.0 claim that all skipped operations stay out of the runtime read: rejected
appends could still be replayed. No merge or release is implied by the follow-up.

## Decisions

1. **Builder.\* returns to legacy-only.** Logic added to `Builder.Data` after the
   2026-07-26 restoration moves out, except for the two small exceptions below
   (D1, D2). Builder.\* changes still pass the legacy parity gates
   (`docs/LEGACY_RESTORATION.md`).
2. **Aurora Legacy stays independent of Reflections.** Its only new dependency
   from this work is `Aurora.Content.Contracts`, a dependency-free package of one
   file, so Legacy keeps applying local corrections as it does today (option B,
   2026-09-19). Legacy takes nothing else.
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
- **D7:** checkpoint commits on the feature branches are approved; each merges to
  `main` as one commit per phase, once parity is demonstrated.

## Current state (verified 2026-09-18)

> **Historical.** This section records what the code looked like before the split,
> and is what the phases were planned against. Phases 4-7 have since carried it
> out: there is no bundled executable, no `Aurora.Importer`, and no v10/v11
> reader. See the phase results below.

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
| **`Aurora.Content.Contracts`** (new, `net10.0`, no dependencies) | Translator | `LocalCorrectionDocument` + evaluation, `ContentReviewContracts`, `CanonicalContentAnalyzer` | `Aurora.Content`, `Aurora.DataIntegration`, `Aurora.Logic` (and through it Aurora.Legacy) |
| **`Aurora.Content`** (new, `net10.0`; Contracts + `Microsoft.Data.Sqlite`) | Translator | See list below | Translator CLI, Aurora.App, Aurora.PdfImport, `Aurora.DataIntegration`, tests/tools |
| **AuroraTranslator CLI** (existing, slimmed) | Translator | Verb dispatch, baseline/regression commands, character-state engine, 5e-API models, `XellarantXmlGenerator` | — |
| **`Aurora.DataIntegration`** (new, `net10.0`) | Lights | See list below | Aurora.App, Aurora.Tests, tools — **not** Aurora.Legacy |
| `Aurora.Logic` | Lights | Gains small dependency-free pieces; its only new package is `Aurora.Content.Contracts` | (unchanged) |
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
- `ElementProvenance`

**A local file is a correction only when it carries the markup** (user, 2026-09-19).
`user/local` also holds plain homebrew, additions and overrides. Decide from
`LocalCorrectionDocument.HasMetadata`, never from the path. `ContentIndexUpdateService`
broke this rule: it refused to replace any `.xml` under `user/local`, reporting
correction metadata that wasn't there, which blocked updates to local homebrew that
carries its own update URL. Fixed 2026-09-19; the importer and library path checks were
already fine, since they only narrow candidates before requiring the markup.

**Legacy keeps applying local corrections (decided 2026-09-19, option B).**
`Aurora.Logic` references `Aurora.Content.Contracts`, a dependency-free package
holding one file. So `DataManager` keeps loading files that carry correction
markup exactly as today, in both apps. Plain local files (homebrew, additions,
overrides) load as ordinary user files, as they always have.
- The alternative was an optional `DataManager` extension point that only
  Reflections registers. It was rejected: Legacy would have loaded correction
  files as plain overrides. With the user's hotfixes, that shows 7 renamed items
  under both IDs and brings back the removed `ID_RDDT_AA_MUSKETBALL`.
- Legacy's package graph gains only `Aurora.Content.Contracts`.
- `AuroraContent.props` supports this with `AuroraContentContractsOnly=true`.

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
| 5 | `Aurora.DataIntegration`: `ElementProvenance` (a `ConditionalWeakTable<ElementBase, string>`) | Set by `DataManager` and the loaders. Check that `ElementBaseCollection.GetFresh` copies keep their provenance. |
| 6 | Stays, rewritten as a self-contained check (D1) | Same behavior; no dependency on `LocalCorrectionDocument`. |
| 7 | Replaced by generic unrecognized-attribute preservation (D2) | Spell-specific cases removed. `SpellAcquisitionResolver` reads the preserved collection. |
| 8 | Restore in `Builder.Data` | If it's noise in Reflections, filter it with the existing `EngineLogNoiseFilter`. |

**Finish line:** `Compare-RestoredAssemblyApi.ps1` reports only the D2 collection
as an addition (down from 265). `Compare-BuilderDataBehavior.ps1` passes against
both builds. `Aurora.Legacy.csproj` builds with `Aurora.Content.Contracts` as its only new package.

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
  - Only consuming projects import this file; **Aurora.Legacy never does**. `Aurora.Logic` imports it
    with `AuroraContentContractsOnly=true`, so Legacy gets the contracts package only.
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
4. **Lights consumes the library.** Add `Aurora.DataIntegration`;
   `Aurora.Logic` takes the correction contracts from the package (decision B,
   replacing the planned `DataManager` extension point); `ContentDatabaseService` goes in-process;
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

   **Step 2 (2026-09-19): reads through the library.** Aurora.Content 0.3.0 adds
   the public `ContentDatabaseReader`: `IsStale`, `ReadMetadata`, `ReadHealth`,
   `ReadLocalCorrections` and `ReadLocalCorrectionContent`. It also makes
   `RuntimeContentFiles` (the Lights copy, verbatim) and the health/metadata
   records public.
   - The app, DataIntegration, tests and tools read through these APIs,
     `ContentDatabase.OpenReadableConnection`, and the library's
     `PreparedCatalogReader`.
   - Aurora.Importer now references the library. It loses its duplicate
     prepared-content reader, health/metadata code and correction read side.
   - What Aurora.Importer keeps retires in later phases: the v10 writer and
     its import-side `LocalCorrectionSync` (tests/tools only), package
     preferences (Phase 5), and the v10/v11 reader helpers
     (`ResolveSourceFilePath`, `SpellcastingExtensionText`,
     `TranslatorSpellcastingReader`) (Phase 7).
   - **Staleness is library-owned.** A database is stale when it's missing, isn't
     a prepared database at the library's current data version, or its recorded
     inputs differ from disk. For a current v12 database the answer is unchanged.
     An old v10/v11 database now shows as out of date, and a refresh rebuilds it
     at v12, per "the app always builds its own database".
   - **Parity:**
     - The full suite on the library's readers matches the Phase 0 baseline:
       databases, projections, checks, 60/60 characters.
     - The pinned 0.3.0 package (Translator `54e8cdd`) passes the database suite.
     - Aurora.Tests 562 pass / 1 skip; Translator tests 68/68.
     - Windows, Android, Legacy and the tools all build.

   **Step 3 (2026-09-19): correction contracts from the package (decision B).**
   - `Aurora.Logic` (`DataManager`, `ContentIndexUpdateService`),
     DataIntegration, Aurora.Importer, tests and tools now use
     `Aurora.Content.Contracts.LocalCorrectionDocument`. Apart from its
     namespace, it's line-for-line identical to the Builder.Data copy, so both
     apps load corrections exactly as before.
   - Only Builder.Data itself still uses its own copy (the `SaveContent` guard,
     `ContentReviewContracts`). Phase 6 removes it.
   - Four files need `ElementsFile` from `Builder.Data.Files` as well, so they
     alias the Contracts type until then.
   - **CI fix found here:** `release.yml` restores each shared library without a
     RID before its `--no-restore` publish. Aurora.DataIntegration (step 1) was
     missing from that list, so its assets file held only the app's
     `net10.0-windows…` target, and the release publish would have failed. Both
     publish jobs now restore it. Replaying the exact release sequence locally
     publishes successfully.

   **Deferred from this phase: multiple roots in one import.** The Translator's
   catalog builder reads one root, and secondary roots keep being composed at
   read time (as before). A multi-root import needs its own precedence and
   correction rules and its own parity check.
5. **Sources and preferences.** Remove package toggles; add the Settings default
   restrictions editor and new-character toggle; load the full catalog; drop DB
   preference reads.

   **Step 1 (2026-09-20): the whole catalog loads; preferences become restrictions.**
   - `DbElementLoader` no longer filters the projection by package preference, so
     every installed source is in the projection the engine reasons over.
   - **One-time migration** (`SourcePreferenceSeed`): sources whose every element
     came from switched-off packages are added to
     `AppSettingsStore.DefaultSourceRestrictions`, and
     `ApplyDefaultSourceRestrictionsOnNewCharacter` is switched on when it
     migrates anything, so new characters keep the user's intent. A source that
     only partly came from a switched-off package keeps loading (a supplement
     often adds a few elements to a book that stays on), and builder
     infrastructure is never restricted. `SourcePreferencesSeeded` marks it done.
     On this machine it resolves to exactly Ryoko's Guide to the Yokai Realms;
     Aurora Legacy Essentials is correctly left alone as infrastructure.
   - **Settings** replaces the package list with the same restriction tree the
     Manage page uses (`SourceRestrictionModelMapper`, now shared), editing a
     dedicated `SourcesManager` so the loaded character is untouched, plus the
     apply-to-new-characters toggle.
   - `GetPackages`, `SetPackageEnabled` and `ContentPackageInfo` are gone from the
     app. The database's `is_enabled` column is now ignored; the writer-side API
     stays in the retiring `Aurora.Importer` for its tests until Phase 7.
   - The rehearsal's disable/re-enable check is now restriction-based, and asserts
     the new invariant: a restricted source stays loaded and stored.
   - **Compendium: no change.** Legacy filters it by nothing at all, and
     Reflections already filters by the open character's restrictions, which is
     the more useful superset. Global defaults are not applied there.
   - **Parity: the first intended departure from the Phase 0 baseline**, reviewed
     item by item. Databases, both fresh projections and all 5 service checks
     still match; the installed projection and every character differ:
     - **6 characters improved, none worsened.** Crow, Honesty and Seraphine now
       load with no missing elements (Seraphine went from 17 to 0); Michelle
       Character 2 from 22 to 3; Fresh E and Michelle Character 1 by one each.
       The known "Art E Claw" diagnostic resolves too: *Claw* is Ryoko content.
     - **Characters gain Ryoko weapon proficiencies** (Claw, Chakram, magitech
       firearms), because Ryoko attaches its weapons to the standard weapon
       proficiency grants. Step 2's Option B filters these for characters that
       restrict the source; until then every character receives them.
     - **New warnings are Ryoko's own content diagnostics** (generic parsing,
       appends targeting `ID_WOTC_PHB_MULTICLASS_MONK`, which this collection
       does not have). They were invisible only because the package was filtered
       out, and belong to the content, not to this change.
     - New reference root for later comparisons:
       `buildtmp/parity-rerun-20260920-041208-e05e32`.

   **Step 2 (2026-09-20): restrictions take content back (Option B).**
   - `IGrantPolicy` / `GrantPolicyContext` in `Aurora.Logic` is consulted both
     when a grant is applied and when grants are re-evaluated. Aurora Legacy
     registers none and grants exactly as before; a policy that throws is treated
     as "grant it", so a host policy can never strip a character's content.
   - `RestrictedSourceGrantPolicy` (Reflections, registered in `MauiProgram` and
     the rehearsal harness) suppresses grants of elements from sources the
     character restricts, and re-applies them when the restriction is cleared,
     because every reprocess re-evaluates grants. It caches the restricted sets
     and refreshes them on `SourceRestrictionsApplied`.
   - `RestrictedSelectionSweep` clears picks whose source the character now
     restricts. The Manage page runs it for both a source toggle and Load
     Default, then saves and reloads the character so the cleared choices come
     back as picks to make again, settled from the saved state (the round-trip
     the user allowed).
   - **The known `validIds.Count == 0` gap is closed:** an empty option pool
     still preserves a pick when evaluation is simply inconclusive, but a pick
     from a restricted source is known to be disallowed and is cleared.
   - **Load validation** now separates elements a character keeps out itself:
     `CharacterLoadValidation.SplitRestricted` reports them as informational, so
     a character saved before a restriction does not read as having lost content.
   - Aurora.Web registers no policy, so it keeps granting everything for now.
6. **Builder.Data cleanup** (the 8 items) and the `Aurora.Logic` additions; legacy gates.

   **Result (2026-09-20): complete.** Builder.Data reports exactly one addition,
   the D2 collection, down from 265. Builder.Core and Aurora.Documents report
   none, the behavior gate passes, and Aurora.Tests 585 pass / 1 skip with full
   parity against the Phase 5 reference.
   - **The API gate was broken before this phase** (`bbfe8c5`). Current Roslyn
     marks every async method's kickoff `[DebuggerStepThrough]`; the compiler
     behind the oracles did not, so all five public async methods in Builder.Data
     read as changed and the gate failed on `main`, reporting nothing useful. It
     now compares that attribute only where source declares it.
   - Items 1–3 and 6 (`d9e5f84`): review contracts and the analyzer to
     `Aurora.DataIntegration`, `LocalCorrectionDocument` deleted, and the
     `SaveContent` guard rewritten self-contained (D1). **Deviation from the
     table above:** the contracts were to go to `Aurora.Content.Contracts`, but
     decision B puts that package in Legacy's graph, and these have no production
     consumer yet, so they stay on the Reflections side.
   - Items 4 and 8 (`a54d3cc`): `RequiredContentPolicy` to `Aurora.Logic` with
     `IsRequiredSource` only (the retiring importer keeps its own package check);
     the "missing 'spellcasting' attribute" warning restored for Legacy and
     filtered in Reflections through `EngineLogNoiseFilter`.
   - Item 7 (`6309b00`): rules keep unrecognized attributes in
     `PreservedAttributes` instead of the parsers carrying a `spell-*` list;
     `SpellAcquisitionResolver` reads from there, with the old setters as
     fallback. This is the one addition Builder.Data keeps.
   - Item 5 (`2ebdb7d`, fixed in `51227e5`): `ElementBase.ContentFilePath` becomes
     `ElementProvenance` in `Aurora.Logic`, keyed by element.
     **`Copy` is a field-level deep clone and cannot carry a table entry**, so the
     synthesized per-class ASI/Feat features lost their provenance — 540 elements.
     The parity suite caught it (characters and checks still matched; only the
     projection dump showed it). The synthesis sites now carry it across, and
     tests cover the rule.
7. **Retire the old paths:**

   **Result (2026-09-22): complete.**
   - The bundled executable, the publish/pin/restore scripts and the opt-in
     integration attribute are gone; the test they guarded imports through the
     library, so that coverage runs in the ordinary suite instead of skipping.
   - Aurora.Importer is deleted. `DbElementLoader` accepts only what the library
     writes and drops from 1516 to 805 lines; an older database is reported as
     needing a refresh rather than read by a second reader. The Settings progress
     types moved to `Aurora.DataIntegration`.
   - `RunImporter`, `AnnotateContentHotfixes` and the rehearsal import through the
     library; the rehearsal's `legacy-import` mode is gone.
   - **Content tests now describe the library**, which surfaced where it behaves
     differently from the retired writer: a padded grant reference stays unresolved
     rather than being matched, conflicting duplicate ids refuse the import, a
     namespaced content root is refused, and an archetype's class is not inferred
     from its supports tag (a gap the library tracks). Real content has no padded
     references (0 of 12,486 grants).
   - **Parity:** databases, both projections, every check and 60/60 characters
     match the Phase 5 reference. (A first run showed one character differing;
     it had timed out because builds were running against the same machine, and
     on its own it matches the reference field for field.)
   - **A test-order hazard is fixed:** the engine's `SourcesManager` snapshots the
     catalog when `CharacterManager.Current` is first touched. The app warms it
     after loading on purpose; tests run in any order, so `ContentFixture` now
     rebuilds the source list when it finds it empty. This was the intermittent
     failure seen during Phase 6.

   - `Aurora.Importer` and the copied writer
   - the v10/v11 reader paths
   - the bundled exe, the pin scripts and `publish-translator.ps1`

   Also point `tools/RunImporter` and the rehearsal harness at the library.
   **Follow-ups found while reviewing Phase 7 (2026-09-23).** Three things the
   review turned up, each fixed where it belongs:

   - **Aurora.Content 0.4.0** (Translator `7a64ed9`): an id reference written
     with surrounding whitespace is stored trimmed, so the grant it names
     actually happens (an element's own id is never rewritten); and a file whose
     root is not an unnamespaced `<elements>` is refused with an error that names
     the file and says what it found, instead of reporting "correction content"
     for a file with no corrections in it. Lights takes it in `30d1806`;
     `ContentDatabaseTrustTests` now describes the fixed behaviour. **Parity:**
     databases, projections, all five checks and 60/60 characters match.

   - **Aurora.Content 0.5.0** (Translator `87f56e6`): an import may be told to
     skip content it cannot use — `ContentImport.ImportAsync(...,
     skipUnusableContent: true)` — instead of refusing the whole refresh over one
     file. A file whose XML cannot be read, one that redefines an element another
     file already declares differently, or one whose corrections cannot be
     evaluated is left out whole; an append operation is narrower, dropping only
     the operation. Declarations are committed per file, so a file that fails
     halfway leaves nothing behind. What was skipped is written to
     `content_skipped_files` and read back with
     `ContentDatabaseReader.ReadSkippedContent`, so it survives a restart, and
     every import re-reads the files: a repaired one stops being listed, an
     unrepaired one is listed again. Skipping stays opt-in, so an import that
     reports success without it has read everything it was given.
     - **In the app:** `SkipUnusableContentOnRefresh` (on by default, in
       Settings › Content) decides, the refresh summary says how many files were
       skipped, and the files are listed with what is wrong with each under the
       refresh controls until they are fixed.
     - The rehearsal's `failure-check` now covers both: with skipping off a
       refusal still preserves the installed database untouched, and with it on
       the refresh completes, names the file, still loads, and clears the report
       once the file is gone. That check's output is therefore a deliberate
       baseline move (17 checks, was 14), as is the new empty
       `content_skipped_files` table in the database comparison.
     - **The harness caught a hole in the first cut** (0.5.0): skipping only
       reached the database. The runtime overlay reads user content from disk on
       every load, so it read the skipped file again and the load failed for the
       very reason the import had skipped it — a refresh that succeeded and then
       would not load, which is worse than refusing. **0.6.0** (Translator
       `1a4a93a`) has `RuntimeContentFiles` honour what the database records and
       leave those files unread. An append skip is not one of them: that file was
       imported and only one operation was dropped.
     - **Still to do** (user, 2026-09-23): auto-fixes for conflicting duplicate
       ids, so the user can resolve one from the list rather than by hand.

   **Parity for both follow-ups (2026-09-23, Lights `3bd9d76`).** Against the
   0.4.0 reference `buildtmp/parity-rerun-20260923-041953-b38161`: both
   projections match, all five checks pass, and the only database difference in
   68 tables compared is the new, empty `content_skipped_files`. The
   `failure-check` output differs by its three new checks and passes 17/17.
   Characters: 59/60 matched in the suite; `Remy Morningstar (Strahd)` ran out of
   its 120 s load budget under four parallel processes, and matches the reference
   field for field when run on its own. That per-character limit is now 5 minutes
   — it is there to catch a hang, not to measure the machine, and it had produced
   a false difference twice.

   - **Source restrictions fall back to the defaults.** A character file with no
     `<sources>` node used to leave whatever the previously loaded character had
     restricted in place. Every character follows some rule about what content it
     may use, and a file that records none has not chosen one, so it now applies
     the configured defaults — as does every new character, which is what
     `ApplyDefaultSourceRestrictionsOnNewCharacter` used to gate. That setting is
     gone: "new characters start unrestricted" is expressed by leaving the
     defaults empty, and empty defaults now clear the previous character's
     restrictions rather than leaving them standing. This is shared engine code,
     so Aurora Legacy follows the same rule.

   **Deferred (user, 2026-09-23):** Aurora.Web never registers a grant policy, so
   restrictions there hide content from lists but do not suppress what a rule
   grants. Web is not the priority; the fix is `GrantPolicyContext.Current =
   new RestrictedSourceGrantPolicy()` at its composition root, as
   `MauiProgram` does.

8. **Full parity run,** then merge to `main`.

   **Result (2026-09-24/25): the content is identical across the version change.**
   Run `buildtmp/parity-rerun-20260924-214506-816c1b`, pinned to the vendored
   0.7.0, against the 0.6.0 reference `buildtmp/parity-rerun-20260923-055401-6ab69b`.

   - **Databases:** 69 tables compared, every row identical — the same 20,876
     elements from 1,189 files. The only differences are the new
     `content_unavailable_elements` table and `database_metadata.data_version`
     12 → 13. The quarantine rule finds nothing to quarantine in real content:
     0 unavailable definitions, 0 skipped files.
   - **Projections:** fresh-a matches; the installed projection matches exactly,
     101,333 entries, nothing on either side.
   - **Checks:** all five pass.
   - **Characters: 60/60.** 59 match in-suite; `Remy Morningstar (Strahd)` shows
     as different only because the *reference* run timed out on it, and the new
     result matches a known-good standalone run of that character field for field.
   - The one systematic difference in every character result is `Missing: []`,
     the field added to `LoadResult` so the partial-load toast can summarise
     instead of printing every id.

   **Two things the run had to work around, both recorded so the next run is
   cheaper:**

   - **A copied database cannot cross a data-version boundary.** The suite runs
     every character against the `installed` case, whose database
     `prepare_baseline.py` copies from the reference. At v12 the v13 reader
     refuses it — correctly, with "Refresh the content database" — so the first
     pass produced 60 identical failures and no comparison at all. Refresh that
     case (`ContentDatabaseRehearsal.exe refresh <root>/installed`) before the
     character part whenever the data version has moved; the `scan` check must
     run before that refresh, since it is about staleness.
   - **The rehearsal's failure-check asserted retired behaviour.** It was written
     at 0.6.0, where a malformed correction file under `user/local` was skippable.
     0.7.0 refuses that in both skip modes, because a truncated document can hide
     a correction section. The check now asserts the refusal in both modes and
     covers the skippable case with an ordinary unreadable file outside
     `user/local`; it passes at 18 checks, which is a deliberate move of that
     one output file.

   **Committed-source 0.7.1 uptake (2026-09-24):** both packages were built from
   the clean Translator `master` commit
   `a84bdf176dc4145cd11bcbe41c903f92d8e9abc4`. That commit changes only the package
   version. The new vendor manifest entry records `dirtySource: false`; both
   package hashes and embedded repository commits were verified.

   - Windows MAUI **Release build passed**, with 0 errors and 24 warnings.
   - **Aurora.Tests: 653/653 passed**, no skipped tests, in Release.
   - All four **legacy gates passed** in Release: Builder.Core 65 required
     signatures, Builder.Data 1,362 with one permitted addition, Aurora.Documents
     166, and Builder.Data behavior tests 10/10 against both source and oracle.
   - Database rehearsal `buildtmp/parity-rerun-20260924-234943-8b572b` used only
     frozen copies from `buildtmp/parity-rerun-20260924-214506-816c1b` and the
     pinned 0.7.1 packages. Both fresh imports succeeded (20,876 elements, 1,189
     files, no skipped files or unavailable definitions). Both databases match
     row for row after excluding build timestamps, both runtime projections
     match exactly, and all five compared diagnostic checks match. All nine
     rehearsal operations returned success; required output files were checked
     explicitly because the comparison script skips absent artifacts.
   - The comparison initially reported a difference in the randomly generated
     failure-fixture directory inside an otherwise identical warning. The
     comparator now normalizes that directory and the rehearsal root, preserving
     filenames, diagnostic text, and IDs. Four Python regression tests pass,
     including changed/missing-warning and failed-check cases. The same captured
     rehearsal results then passed comparison; no application behavior changed.
   - The shell wrapper hit a local build-permission failure; its stages were run
     separately with a single-worker harness build (`-m:1 -nr:false
     -p:UseSharedCompilation=false`), followed by the unchanged database suite and
     the corrected comparator. Logs are in `buildtmp/verification-0.7.1`.
     Character parity was not repeated for this version-only uptake.

Later / optional:
- **Refine the same-name selection restriction** (user follow-up, 2026-09-24):
  keep the current picker rule for this release to prevent selecting both PHB
  2014 and PHB 2024 printings of a spell, but review how alternate printings are
  identified and enforced. A shared name alone does not prove functional or
  referential equivalence: unrelated homebrew or third-party definitions may
  collide by name. Investigate explicit printing/equivalence metadata rather than
  broadening name-based identity. Preserve separate spellcasting acquisition
  domains, repeatable selections, the ability to replace the current choice, and
  existing saved/granted elements; the current rule only disables picker options.
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
- The Translator's uncommitted work predates this plan. Phase 0 must capture it
  before any `git mv`.
