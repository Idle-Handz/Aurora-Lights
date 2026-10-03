# Aurora.Content 0.8.1 consumer uptake

Local integration completed September 25, 2026. No commit, app release, deployment,
or live content database refresh was performed as part of this uptake.

## Packages and provenance

- Copied the existing `Aurora.Content.0.8.1.nupkg` and
  `Aurora.Content.Contracts.0.8.1.nupkg` release artifacts from Translator's
  `artifacts/packages/0.8.1` into `vendor/nuget`, without repacking them.
- Verified package IDs, versions, embedded repository commit, and SHA-256 hashes
  against the release manifest before copying, and verified copied hashes afterward.
- Both packages identify clean source commit `ce612682dc1bcb69027a3ef1527b6d3cac78e79b`.
  [The consumer manifest](../vendor/nuget/manifest.json) records this provenance and
  retains the earlier experimental 0.8.0 entry unchanged.
- [The shared pin](../AuroraContent.props) now selects 0.8.1. Existing public API
  call sites compiled without adapter changes.
- The package uses database schema 1 and data version 15. It adds typed declaration
  and append validation, unreadable-input handling, and preservation of prior
  effective definitions when append suppliers cannot be read; see
  [the Translator handoff](../../5eApiTranslator/docs/aurora-translator-data-handoff.md).

## Consumer verification

- Dependency restores passed for `Aurora.Tests` and `Aurora.App` and their project
  references. The app restore covers its Windows and Android targets on this host.
- **104 focused tests passed, zero failed or skipped**, covering import reporting,
  unsafe-fallback protection, database trust/recovery, prepared projections,
  correction metadata/lifecycle/provenance, padded references, and PDF inference.
- Tests used the vendored 0.8.1 packages with `--no-restore -m:1`, not the sibling
  source-project override. Build warnings were the existing CS8629 and xUnit1031
  warnings in unrelated tests. NuGet auditing was disabled for these restores;
  this was not a vulnerability assessment.
- Logs: `artifacts/content-0.8.1-restore-tests.log`,
  `artifacts/content-0.8.1-restore-app.log`, and `artifacts/content-0.8.1-tests.log`.
  Test results: `artifacts/content-0.8.1/content-0.8.1.trx`.
- No platform application build or device run was performed. Test imports use
  disposable fixtures; installed XML, live content databases, and character saves
  were not refreshed or modified.

## Remaining deployment steps

Include this uptake in the next application release and install that build. Close
character tabs, then use **Settings > Content > Refresh Database** to rebuild an
older database with the updated importer. An XML download alone cannot replace the
library embedded in an existing app. Clearing app data is unnecessary.
