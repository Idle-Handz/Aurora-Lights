# Recent update review — September 25, 2026

Implementation follow-up: both findings are fixed in the local working tree. Unsafe raw XML fallback now stops with an explanatory error using the library's persisted exclusion records; source filtering now follows matching ancestors and descendants. The focused post-fix run passed 46 tests, including the permanent regression cases in `ContentFallbackPolicyTests` and `SourceRestrictionsEditorTests`. See `review-fixes.trx` and `../review-2026-09-25-fixes.log`. Changes are uncommitted, and Android device execution was not performed. The original review below records the pre-fix findings and evidence.

Reviewed the September 24–25 changes through `e93fa80`, plus the existing uncommitted content-library 0.8.0 integration and Android layout updates. The review concentrated on character persistence, selection availability, source restrictions, content loading, and their shared App/Web callers. It does not establish that the entire codebase is bug-free.

## Confirmed findings

### P1: Cold-start XML fallback bypasses retained-definition decisions

Location: `Aurora.DataIntegration/ContentDatabaseService.cs:38–46`, called by `Aurora.App/Services/CharacterService.cs:141–142`.

The 0.8.0 policy keeps a last-known working definition when current declarations conflict. Such IDs are usable, so `ReadUnavailableIds` does not return them. `ValidateRawXmlFallback` therefore permits the old raw XML loader after a prepared load fails, even though that loader does not honor retained/rejected declaration records.

Reproduced with the vendored package and actual loader services:

1. Import an element named `Original retained`.
2. Change its supplier to `Changed A`, add a conflicting supplier `Conflicting B`, and refresh with skipping enabled.
3. Confirm the database projection still yields `Original retained` and reports `definition-collision`.
4. Add an unrelated malformed file under `user/` and simulate a cold start with an empty live catalog.
5. The prepared load fails with an XML parsing error. The fallback guard allows loading raw XML, which replaces the retained definition with `Conflicting B`.

This can silently change character mechanics according to file order. Extend fallback protection to persisted resolution/exclusion decisions, or use a recovery path that preserves those decisions.

### P2: Source search loses matches at category/publisher boundaries

Location: `Aurora.Components/Shared/SourceRestrictionsEditor.razor:183–198`.

`Matches` checks the current label and source names, but not child publisher labels. `VisibleChildren` independently filters children without preserving a matching parent category. With multiple publishers under `5e official`, searching `Wizards of the Coast` hides the category altogether; searching `5e official` shows an empty category. In both cases Player's Handbook becomes inaccessible in the filtered tree despite matching the intended branch.

Both cases were reproduced with the actual component in bUnit. Propagate matching descendants upward and matching ancestors downward when determining visibility and expansion.

## Validation and evidence

- Existing focused suite: **123 passed, 0 failed, 0 skipped**. Covered portraits, prepared-spell lookup, selection availability/resolution, source refresh/tree/editor/required sources, import reporting/exclusions/progress, partial loads, and PDF inference.
- Additional reproductions: **3 failed as expected**, confirming the two issues above (one loader scenario and two search queries).
- Test execution used `dotnet test Aurora.Tests/Aurora.Tests.csproj --no-restore -m:1` with class filters. No whole-solution or Android device run was performed.
- No production fixes were applied. The temporary test source was removed from the active test project and preserved beside this report as `RecentUpdateReviewReproTests.cs`.
- Baseline result: `recent-update-review.trx`.
- Reproduction result: `review-reproductions.trx`.
- Console logs: `../review-2026-09-25-tests.log` and `../review-2026-09-25-reproductions.log`.

To repeat the reproductions, temporarily place the preserved source in `Aurora.Tests/Tests/`, then run the test project with `--filter FullyQualifiedName~RecentUpdateReview`. Its fixtures redirect content to a unique temporary directory and restore singleton state afterward.
