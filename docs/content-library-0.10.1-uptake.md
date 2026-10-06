# Aurora.Content 0.10.1 adoption

Consumer verification on October 5, 2026. This adopts the shared library from
0.9.0 to 0.10.1; it does not switch the runtime loader to the new optional catalog
reader API or establish a startup performance improvement.

## Packages and behavior

Both immutable release artifacts were copied from Translator's
artifacts/packages/0.10.1 without rebuilding. Package IDs, versions, embedded
repository revision and SHA-256 hashes were checked before and after copying.

- Source: e6dfacdb12162c37cd9e9d5387b5d8832173e389, clean release.
- Aurora.Content.Contracts: 75eff3e8d46da4c4851a541f6b61c36f9ac629bf203e9e94a4c7f96abad60109.
- Aurora.Content: ecb57369e0d45c54a0bcb72336e3a1722f4622e135c7b1aecf31d32dce9cc160.
- Schema remains 1; data advances 17 to 18, preparation contract 1 to 2.
- Append operations follow Legacy relative-path order, including supplements
  before homebrew. Appended descriptions no longer replace or extend base prose,
  including a target with no description. Original append XML remains inspectable.
- Successful XML import recomposes the database before advancing its data version.
  Unsafe retention of old append effects blocks refresh and preserves the database.

## Verification

- Full Release Aurora.Tests suite: **857 passed, zero failed or skipped**.
- Windows app, Android app and web host builds passed. Existing compiler warnings
  remain; these are build checks, not new rendered UI or device acceptance.
- Legacy API checks: Builder.Core 65 required signatures, Builder.Data 1,362
  required signatures with five permitted additions, Aurora.Documents 166 required
  signatures. The three restored assemblies are byte-identical between the
  baseline and updated harness outputs; the permitted additions predate adoption.
- Builder.Data behavior: **10/10** against restored source and **10/10** against
  the production Legacy oracle.
- Direct Legacy append oracle: both present/absent base-description cases pass.
  Four new consumer regression cases cover persisted/runtime appends and repeated
  replay, with original append provenance retained.
- Policy lifecycle rehearsal: **22/22 phases, 225 assertions**, each in a separate
  process, across both skip-content settings. The successful best-effort conflict
  refresh assertion was stale under both 0.9.0 and 0.10.1; it now expects the
  Legacy-selected later declaration. Strict rejection still expects the previous
  definition, and all database/correction preservation assertions remain.
  The cold invalid-correction startup probe remains observational: loading fails
  and the database is preserved, but it does not claim raw fallback is blocked.
- Genuine old-package fixtures: data17/preparation1 migrates to data18/preparation2
  with unchanged input XML; descriptions and grant order match the new policy.
  A malformed old append supplier rejects migration with repair guidance and an
  identical database SHA-256 before/after failure. Repair, retry and fresh-process
  reopen pass. Repeated import matches all 74 tables after timestamp normalization;
  migrated/repaired versus fresh imports also match after normalizing source file
  surrogate IDs to their relative paths.

- Whole-corpus refresh migrated the real copied data17 database to data18 and
  preparation2. Its 24,410-element runtime projection equals a fresh import.
  Two independent fresh imports match all **74 tables**, with SQLite integrity
  ok and zero foreign-key violations.
- Old versus new runtime catalogs have zero lost/new IDs, changed declaration
  winners, names or types. The 192 changed fingerprints are explained by 188
  support-list order changes, two proficiency grant-order changes, Psion appended
  description removal, and its generated multiclass counterpart.
- Corrected declaration-winner comparison against Legacy: **zero missing from
  either side, 806 label-only differences, 574 definition differences** under
  both versions, with the exact same differing-ID set. These XML fingerprints
  do not establish runtime equivalence because Legacy mutates parsed append
  properties without updating the base ElementNode; the separate parsed-property
  oracle and character comparisons provide the behavioral checks.

- Complete paired character suite: **60/60 results compared, zero changes or
  regressions**. Both versions have 49 fully successful results and the same 11
  results with existing issues. Captured state, choices, inventory, missing
  elements and missing grants match; original character copies remain unchanged.
  Lola Bunny has zero missing elements, Rochelle Lee has zero, and Rochelle Lee
  (Revised) has two, on both the first and second load under both versions.
- Six alternating fresh-process service loads (three per version) had median
  elapsed times of **10.99 seconds for 0.9.0** and **10.85 seconds for 0.10.1**,
  with the same loaded runtime count. This small observation does not establish
  a performance improvement and is not a rendered app startup benchmark.
- Final preservation verification checked **1,250 installed input files**:
  unchanged, with no added files. Existing unrelated work in the primary checkout
  remains outside the adoption commit.

## Corpus isolation and known limitations

The installed corpus was copied using a consistent SQLite backup and relocated
paths. Aurora's index updater checked the disposable copy before baseline
measurement: 1,353 entries across 92 indexes, zero updates and zero failures.
Both libraries use the same 1,190 XML files and 60 character copies. The old
harness was built before changing the pin, and the new installed case began with
the genuine refreshed data17 database.

The complete XML/database parity diagnostic is blocked under **both** versions
by the same pre-existing runtime collision:
ID_WOTC_XGTE_MAGIC_ITEM_STAFF_OF_FLOWERS, from the DMG 2024 and Xanathar sources.
This is not a newly introduced failure or a claim that full XML parity passes.
Installed XML, the production database and user character saves are not changed.

The migrated database also passes the shared library's integrity validator.
A fresh import reports the same pre-existing metadata count warning under both
0.9.0 and 0.10.1: metadata counts 20,945 effective definitions while the physical
elements table also retains one superseded declaration (20,946 rows). The
migrated metadata counts all physical rows. Neither version gains a stale active
definition. Historical package/correction/cache metadata remains different from
a clean import. Text-normalization differences shrink from 602 to 464, with no
new differing rows; raw migrated/fresh SQL equality is not claimed.

The adoption pipeline was executed as explicit verified steps because
take-content-library.ps1 creates its own branch and rebuilds packages instead
of consuming the immutable verified release artifacts. Git Bash is required by
the shell rehearsal scripts; Windows' default bash resolved to incompatible WSL.
The initial shell invocation produced no valid suite evidence; it was rerun
through Git Bash. Individual JSON reports and result completeness are checked,
rather than relying on the shell runner's aggregate exit code.

## Installing the update

After installing an application build containing 0.10.1, close character tabs and
refresh the content database. A data17 database must be recomposed from XML;
changing a SQL version stamp is insufficient. If refresh reports an unreadable
supplier, repair it and retry. Clearing app data or deleting character saves is
unnecessary.

## Local evidence

All logs, package/migration evidence and comparison reports are retained under
buildtmp/take-0.10.1 in the content-library-0-10-1 worktree.
The frozen baseline is buildtmp/parity-baseline-20261005-182812-47e624; the upgraded
run is buildtmp/parity-rerun-20261005-183235-d41e94. These ignored files are local
evidence, not published release artifacts.
