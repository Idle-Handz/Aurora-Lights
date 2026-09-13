# SQLite import ownership audit

Audited 2026-09-12. Runtime XML ingestion is outside this audit's replacement scope.

## Current application behavior

- `Aurora.App/Services/ContentDatabaseService.cs`: `SyncAsync` launches the bundled Windows Translator with `sqlite-import` only when exactly one content directory is configured and the executable exists. Otherwise it calls `AuroraContentImporter.Import`. A Translator process failure returns a failed result; it does not invoke the copied importer afterward.
- `Aurora.App/Services/ContentService.cs`: reload checks staleness and syncs before reloading elements. It does not inspect the returned sync result. Consequently, a failed sync does not itself force XML loading: the loader may still accept an existing database.
- `Aurora.App/Services/CharacterService.cs`: a failed database load invokes runtime XML loading. Successful database loads also receive raw user XML overlays and an unsynced-content merge in `DbElementLoader`. Those are runtime ingestion, not SQLite writes.
- `Aurora.Importer/AuroraSqliteImporter.cs`: contains the copied XML-to-SQLite importer, schema setup, version-based reimport, and resolution logic. It still writes data version 10.
- `Aurora.Importer/AuroraContentImporter.cs`: package enable/disable writes `content_packages` and calls `RebuildCacheOnly`, which rebuilds resolution caches and deferred relationships using copied logic. This is a second production SQLite mutation path, separate from full import.
- `tools/RunImporter/Program.cs`: developer utility invokes the copied importer directly. `ContentDatabaseTrustTests` also use it to build test databases.
- `tools/publish-translator.ps1`: publishes the sibling Translator project into the app's bundled tools directory. It does not validate reader compatibility.
- Compendium and PDF character inference query the database; they do not build or refresh the content database. SQLite rollback-journal recovery in `OpenReadableConnection` is recovery, not an import.

The executable-only architecture is therefore not yet implemented. Removing `Aurora.Importer` wholesale would also remove metadata, health, staleness, package management, and connection helpers used by the app.

## Snapshot compatibility change

Lights accepts data versions 10 and 11 with schema version 1. Version 10 continues using the existing reader. Version 11 reconstructs spellcasting from Translator's complete `raw_xml`, preserving ordered children, expressions, `known`, and `all` through the existing runtime parser. Missing or malformed spellcasting XML fails the database load, requesting XML fallback. A database load that skips elements also fails instead of reporting partial success.

The local writer remains version 10; it must not advertise version 11 without implementing that format. No general forward-compatibility promise or reader-contract redesign is introduced here.

## Follow-up for executable ownership

1. Make SQLite creation/refresh exclusively a Translator operation. Missing/unsupported executable or failed sync should select runtime XML explicitly, including when an older readable database exists.
2. Resolve multiple-directory support before removing the current fallback. The CLI currently accepts one root. Running it once per root against the same database is unsafe because imports remove files absent from the current catalog.
3. Choose packaging for other supported platforms, or intentionally use runtime XML there. Current bundled executable selection is Windows-only.
4. Route package mutations and their cache refresh through Translator as well, preserving existing enable/precedence behavior.
5. Retain or extract read/health/recovery helpers; retire copied writer code and its developer utility only after callers migrate.
6. Add the separate reader compatibility contract and publishing check afterward.

This audit does not change sync ownership, packaging, or package mutation behavior. A shared importer library is an alternative to executable ownership, not a prerequisite for it.
