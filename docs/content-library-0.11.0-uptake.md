# Aurora.Content 0.11.0 adoption

Reflections advances from 0.10.1 to 0.11.0 on October 6, 2026. Both shared
packages use the single pin in `AuroraContent.props`.

## Packages and compatibility

The immutable packages were copied from Translator's `artifacts/packages/0.11.0`
without rebuilding. Package identities, embedded source revision, dependencies,
and SHA-256 hashes were checked; the copied hashes match the release record.
`vendor/nuget/manifest.json` records this provenance and preserves earlier versions.

- Source commit: `1796636803dd21e5548393070e84e6ecf9ee8695`.
- Contracts SHA-256: `15ed245ed186e08a41dbce2de0be2ace31d117c8ed02a9e97bf651c25292bf72`.
- Content SHA-256: `756be3267f2ee68db954a3c51334f5a8584a87bc6b64d7d5f6843789e53331ab`.
- Schema remains 1 and preparation contract remains 2; data advances 18 to 19.
- Content depends on Contracts 0.11.0 and Microsoft.Data.Sqlite 10.0.12.

The release adds correction editing/review APIs and durable `approved-local`
state. Content Doctor now uses `IncorporatedKeys` to recognize corrections already
present upstream, including approved corrections that no longer emit pending
review messages. Active approvals display as **Approved locally**. Existing
explicit upstream acceptance still leaves file retirement to a separate import.
Release review caught a disabled-file edge case in the typed incorporation result:
Content Doctor now labels disabled corrections, hides their acceptance action,
and checks the actual file and reviewed hashes before accepting a correction.
The service refuses acceptance even if a caller changes the presentation flag.

The new correction editor and group approval UI are not part of this migration.
The existing per-correction clearing action cannot accept a multi-member group;
the library rejects partial acceptance, as it did before this upgrade.

## Database refresh

Existing data-18 and older databases require an XML import. The app already uses
the library's current data version for staleness and load checks, and its refresh
service calls `ContentImport.ImportAsync`. No host-side version constant or SQL
migration is needed. Do not make an older database current by changing metadata.

After running a build containing 0.11.0, refresh content in Settings to rebuild
the database with data-19 correction review semantics. This adoption does not
refresh installed content or alter a user's correction files or database.

## Focused verification

The Windows Debug Reflections build passed with zero errors (32 warnings in
unchanged code). The focused `Aurora.Tests` run passed **61 tests, zero failures
or skips**, covering `ContentDoctorServiceTests`, `LocalCorrectionLifecycleTests`,
`PreparedContentProjectionTests`, `ContentChangeScanTests`, and
`ContentLibraryUpgradeTests`.

```powershell
dotnet build Aurora.App/Aurora.App.csproj -f net10.0-windows10.0.19041.0 --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test Aurora.Tests/Aurora.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ContentDoctorServiceTests|FullyQualifiedName~LocalCorrectionLifecycleTests|FullyQualifiedName~PreparedContentProjectionTests|FullyQualifiedName~ContentChangeScanTests|FullyQualifiedName~ContentLibraryUpgradeTests"
```

Restore used the vendored feed and cached dependencies with NuGet audit disabled.
The initial focused pass was followed by the release-readiness checks below.
Local logs and test results are in `buildtmp/content-library-0.11.0/`.
These checks ran locally before publication; no application release was published
or installed as part of validation.

The migration regression explicitly simulates data-18 metadata on disposable
unchanged-schema content; it is not a fixture produced by the old package or a
production database rehearsal. It checks stale detection, app loader refusal with
refresh guidance, preservation of the working catalog, and successful import and
reload with the source element retained.

## Release-readiness validation

The full Release `Aurora.Tests` suite passed **895 tests, zero failures or skips**.
The Release WPF suite passed **5 tests**. The full run compiled before the
disabled-file guard was added; the subsequent Release run of the same five content
test classes passed **63 tests, zero failures or skips** on the final code,
including both new disabled-file regressions. That run used
`-p:BuildProjectReferences=false` with the already-built Release dependencies.

The existing browser suite ran against the built Release Web host with isolated
session storage: **15 passed, one intentional mobile duplicate skip, zero
failures**. It covers seven routes on desktop/mobile and the legacy character
import and Build picker flow. The release-note compiler's **3 tests** passed.

Legacy release checks passed: **17 API-comparison helper checks**, zero missing
required API signatures (Core 65, Data 1,362, Documents 166, Presentation 23),
and production-oracle behavior comparisons of **10/10**, **8/8**, and **5/5** for
Data, Documents, and Presentation. Data's five permitted additions predate this
migration.

A package-only rehearsal used separate processes and actual vendored 0.10.1 and
0.11.0 packages, with no source project references. All **65 assertions** passed:

- The old package created schema 1/data 18/preparation 2; the new package required
  refresh and imported it to data 19 with source XML and effective corrections
  retained.
- Invalid protected correction metadata refused migration even with skipping
  enabled. The previous database hash was unchanged; repair and retry succeeded.
- Approved-local state survived import and a fresh process. A relevant upstream
  change reopened review without rewriting or retiring local XML.
- Explicit upstream acceptance alone retained the file. Successful import then
  retired it to a byte-identical recoverable copy.

Windows x64 and ARM64 self-contained Release publishes passed with the release workflow's
`PublishReadyToRun=false` and `WindowsPackageType=None` settings. Both packaged
content assemblies report `0.11.0+1796636803dd21e5548393070e84e6ecf9ee8695`.

Android ARM64 Release publish passed with trimming enabled and local test signing
(`AndroidKeyStore=false`), producing APK and AAB files. `aapt2` confirmed
`com.auroralights.app`, target SDK 36, and `arm64-v8a`; `apksigner` verified the APK
using v2 and v3 signatures. These artifacts retain development application version
1.0/versionCode 1 and are validation outputs, not production release artifacts.
The first sandboxed attempt could not start MSBuild's linker task host; the
same publish succeeded outside that restriction without a source workaround.

The macOS targets, production signing, Velopack installers, app installation,
and native-device/UI acceptance remain for CI and release acceptance. No native
application was launched against installed content, and no production database
was refreshed. NuGet vulnerability-feed auditing was not performed during the
offline restores. The release workflow still requires its complete platform
matrix and tests before creating a release draft.

The new release-note fragment `release-process/content-data-version-19-upgrade.md`
instructs users to refresh older databases after installing the updated app.

Evidence, exact commands, package-only harnesses, browser reports, and TRX results
are under `buildtmp/content-library-0.11.0/release-readiness/`, including the
`migration/commands-and-results.txt` and `legacy/commands-and-results.txt` indexes.

## Integration with current main

Before pushing, the migration and existing UI-thread content probe were rebased
onto `e637e8f` (the Shop merge) without conflicts. Content Doctor's rarity
suggestions and override authoring remain alongside the typed incorporation and
disabled-file protections.

The combined Release code passed **286 tests, zero failures or skips**, covering
Content Doctor, override authoring, rarity repair, the database upgrade, prepared
content, local correction lifecycle, and Shop tests. The Windows x64 Release app
build also passed with zero errors and two existing nullable warnings in
`BuildService.CustomFeatures.cs`. Logs and TRX results are in
`buildtmp/content-library-0.11.0/post-rebase/`.
