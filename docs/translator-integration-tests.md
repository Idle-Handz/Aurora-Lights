# Translator integration testing (retired)

The app no longer runs a Translator executable. Content import happens in process
through the shared `Aurora.Content` library, so there is nothing to publish beside
the app and no external-process integration to arrange. The scripts that published,
pinned and restored that executable are gone, along with the opt-in test attribute
that skipped unless one was supplied.

What covers this ground now:

- **`Aurora.Tests`** builds databases with `ContentImport.ImportAsync` directly, so
  writer coverage runs in the ordinary suite instead of being skipped
  (`PreparedContentProjectionTests`, `LocalCorrectionLifecycleTests`).
- **`tools/ContentDatabaseRehearsal`** exercises the app's own refresh, load,
  parity and failure paths against disposable copies of real content.
  `verify_content_library.sh <baseline-root> <label> [db|characters|all]` builds
  the harness against a library build — a sibling checkout, or the vendored
  package with `AURORA_CONTENT_SOURCE=pinned` — runs the suite and compares
  everything against a baseline root.
- **The library's own tests** in the AuroraTranslator repository cover import,
  preparation and the correction workflow.

See `docs/translator-library-split-plan.md` for how the split was carried out.
