# Temporary prepared-content bridge

Origin: the uncommitted Translator v12 proposal reviewed on 2026-09-14, based on
Translator HEAD `21070b03a561343b17b563ae90d8a23ef00ab29a`.
The complete proposal and source hashes are in the sibling repository at
`docs/proposals/reflections-prepared-content-v12.patch` and
`artifacts/reflections-prepared-adapter/manifest.json`.

`ContentText.cs` and `ContentAppendComposer.cs` match the sibling
`5eApiTranslator/Content` copies after normalizing line endings. Current raw
Lights file SHA-256 hashes:

- ContentText: `82F3B66B9C46A8CC1727BBB9937C4EFE004487DBBCD1B793606B10BE7F9A14FF`
- ContentAppendComposer: `8D1AF8CDC305B9A0FA71CFD0963372765DBCEDB7B49360872DB9C628A87544F7`

`PreparedCatalogReader.cs` has consumer-specific changes: replacement XML files
participate before append replay, database/runtime operations share file ordering,
different same-ID definitions fail rather than winning by load order, and unknown
preparation markers remain protected from the old writer. `RuntimeContentFiles.cs`
is Lights-specific folder/correction integration. Port these requirements and
their focused tests into the shared library; do not blindly overwrite the reader
with a future Translator copy.

Runtime correction composition must retain the local supplier for new add
definitions while retaining the authoritative supplier for replacements. This
matches Translator preparation and prevents a local variant from inheriting the
authoritative file's enable preference. The full-corpus rehearsal and regression
are documented in docs/content-database-rehearsal-2026-09-15.md at the repo root.

This directory does not implement database creation. The near-term shared-library
migration must include correction preparation, append composition, candidate
validation, SQLite writing, activation and retirement. Users start with essentials
and bring their own XML; first-run database creation must work on each supported
platform without a separate executable or a prebuilt user-content database.
