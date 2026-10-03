# Resilient content refresh

**Package follow-up:** Lights now pins the clean committed 0.8.1 packages (schema 1,
data version 15), with 104 focused consumer tests passing. See [the uptake record](content-library-0.8.1-uptake.md)
for provenance and deployment steps. The 0.8.0/data-14 verification below records
the earlier implementation checkpoint.

## Best-effort imports and established successors — September 25, 2026

This policy supersedes the September 24 blanket conflict rejection below. Implementation targets Aurora.Content / Aurora.Content.Contracts **0.8.0**, schema **1**, data version **14**. The additional data version prevents older readers from silently ignoring retained/rejected-definition decisions. Refresh an existing prepared database to migrate; no installed XML is rewritten.

- Prefer AuroraLegacy over archived aurorabuilder definitions of the same exact ID when their XML update-file URLs identify `AuroraLegacy/elements` and `aurorabuilder/elements` on `raw.githubusercontent.com`. This is the explicitly approved repository succession rule, not a general trust score. It does not decide between unrelated repositories or divergent AuroraLegacy candidates. Distinct archived definitions remain available. New versions from the established successor can replace earlier accepted definitions.
- With skip enabled, inspect every declaration before selecting. Ambiguous exact-ID collisions keep the previous **effective XML**, including previously applied append effects. Unaffected definitions update normally. Do not apply current append operations to retained definitions: that would silently change the supposedly preserved mechanics.
- With no previous working definition, select a **provisional** candidate in ordinal order of normalized relative path, then declaration position (absolute path is only a final tie-break for multiple roots). This is deterministic, not a claim that the choice is authoritative. Log and persist the competing declarations as skipped collisions for later review. Identical declarations still consolidate silently with their provenance retained.
- Retention survives restarts and repeated refreshes, including a supplier becoming unreadable or all known suppliers becoming unreadable. Preserve cached effective definitions when available; no synthetic declaration is claimed to have come from broken current XML. Remember unreadable supplier paths until repair/removal permits convergence. A successfully parsed sole remaining declaration, identical converged declarations, or an established successor releases retention.
- An unreadable file's cached content can be recovered without importing its malformed current bytes. The current file is still reported unreadable. Never rewrite source files or promote an alternative merely because a competing file failed to parse.
- Correction protection is unchanged: invalid metadata, ambiguous correction origins, inconsistent review groups and conflicts involving explicitly protected IDs still block activation. A provisional/retained companion prevents retirement of its local correction file until resolved. Local XML is not automatically an override.
- Case/padding variants are still unavailable rather than silently rewritten into one ID. Unrelated content still imports. With skip disabled, unresolved first-import conflicts remain unavailable and new unresolved refresh conflicts still block. The established AuroraLegacy succession rule applies in both modes.
- Aggregate index directories are containers, not publishers. Classification uses a file's own Source declaration, referenced publication, or nearest containing Source declaration; never all unrelated Source records under an aggregate root. Ambiguous publisher flags in skip mode retain game content and report a classification issue. Missing/ambiguous classifications use the existing neutral `local` database bucket, without claiming homebrew/official authorship or treating the content as a correction.

Persistence and reader contract:

- `content_declaration_provenance` retains current valid accepted and rejected declarations.
- `content_rejected_declarations` records supplier path, captured hash, ordinal, ID and XML for runtime exclusion of that exact rejected revision.
- `content_definition_resolutions` records provisional/retained/successor decisions, the chosen supplier and effective XML. `content_definition_suppliers` keeps paths needed for retention across unreadable refreshes.
- `content_prepared_elements` contains the actual selected/retained definition. `content_skipped_files.kind` now also reports `definition-collision`, `superseded-definition` and `classification`; these are **not whole-file exclusions**. `unreadable` still excludes the captured broken runtime file. Consumers must not discard an entire file for a declaration-level collision.
- Runtime XML reads omit the exact rejected revisions and restore retained effective definitions. New/different XML revisions are re-evaluated, not silently dismissed by a stale path-only decision. Appends to retained definitions remain recorded as skipped so they cannot replay at runtime.
- Settings distinguishes review issues from successful successor replacements and limits the initial issue display to 30 entries, with access to the full report. SQLite remains the performance cache; ordinary ambiguity must not prevent best-effort availability.

Validation: focused library fixtures cover provisional selection, same-file collisions, unaffected content, effective-XML retention, data-13 migration, repeated imports, unreadable suppliers, repair, authority updates, SQL/runtime parity, aggregate classification, correction protection and retirement. An isolated fixture containing the actual public Oathbreaker and original/Legacy DMG Source files imports 12 elements and classifies Oathbreaker as official. This is not a full master-index or Android-device run. Package provenance must remain marked dirty until source changes are committed and a fresh immutable version is vendored for release.

### Aurora Lights verification

The vendored 0.8.0 packages passed **41 focused Aurora.Tests tests** for import reporting, database trust, prepared projections and correction metadata. The updated `tools/ContentDatabaseRehearsal/run-policy-checks.ps1` passed **225 assertions in 22 separate processes**, exercising the actual sync and full-loader services with skipping both enabled and disabled. First-install choices, retained definitions, unaffected updates, repair, restart persistence and protected-correction rejection all behaved as intended. Results: `buildtmp/content-policy-smoke-20260925-134305-4d3dae/summary.json` in Aurora Lights. The focused shared-library tests and actual upstream Oathbreaker fixture also pass. Live content, settings and character saves were not modified.

The full recursively fetched master index still needs an Android-device refresh with this build. The installed older APK cannot acquire the new policy solely through a content download. Do not clear app data again to address these known library failures.

Android ARM64 Debug build also passed: dotnet build Aurora.App/Aurora.App.csproj -f net10.0-android -r android-arm64 -c Debug -m:1 -p:NuGetAudit=false (0 warnings, 0 errors). Device runtime and the full master-index refresh remain unverified.

