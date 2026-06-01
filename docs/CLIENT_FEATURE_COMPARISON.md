# Aurora Client Feature Comparison

Last reviewed: June 1, 2026

This is a living comparison of the three Aurora clients in this repository. It is
intended to help players choose the right client and help contributors understand
where a feature belongs.

The table is deliberately conservative. A feature marked `Partial` is real and
usable, but it should not be read as a claim of complete desktop parity.

## Client Roles

| Client | Project | Primary role | Current audience |
| --- | --- | --- | --- |
| Aurora.Lights | `Aurora.Lights` | Keep the familiar legacy Windows application alive on updated frameworks while preserving its established workflows. | Existing Aurora users and contributors working close to the legacy application. |
| Aurora: Reflections | `Aurora.App` | The primary modern desktop evolution, with a more session-friendly interface and a SQLite-backed content cache. | Players who want the actively developed modern desktop experience. |
| Aurora.Web | `Aurora.Web` | A first step toward a browser-hosted builder. It currently provides temporary session workspaces and focused editing flows. | Contributors, testers, and players exploring an early online direction. |

## Status Legend

| Status | Meaning |
| --- | --- |
| Established | A mature legacy workflow exists. Modernization work can still expose bugs. |
| Available | Implemented as a normal supported workflow in that client. |
| Partial | A useful implementation exists, but it is narrower or still needs parity work. |
| Experimental | Present for testing, with more rough edges expected. |
| Not yet | No equivalent workflow is currently exposed. |
| Not applicable | The client intentionally solves the problem differently. |

## Installation, Platforms, And Storage

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Primary platform | Established: Windows desktop. | Available: Windows desktop is the recommended build. Mac Catalyst and Android builds are experimental. | Available: modern web browser. |
| Installation | Build or run the Windows application directly. | Velopack installer is the preferred Windows installation path. Portable builds can run without the full update experience. | No local install when hosted. |
| Character persistence | Available: local `.dnd5e` character files. | Available: local `.dnd5e` character files. | Partial: characters live in a temporary anonymous session workspace until downloaded. |
| Legacy data directory | Available: uses the familiar `Documents\5e Character Builder` directory. | Available: intentionally shares the legacy directory by default so existing characters and custom content remain visible. | Not applicable: uses temporary server-side session storage. |
| Custom content source of truth | Established: Aurora XML content. | Available: Aurora XML remains authoritative. `custom\aurora-elements.sqlite` is a rebuildable cache. | Experimental: temporary `.xml`, `.zip`, and `.dnd5e` uploads can be indexed for the current session. |
| User accounts or cloud saves | Not yet. | Not yet. | Not yet: the current browser workspace is intentionally anonymous and temporary. |
| Backups | User-managed local backups remain important. | User-managed local backups remain important, especially while sharing a directory with the legacy app. | Download the `.dnd5e` file before the session expires. |

## Character Library And Files

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Character library | Established: local character library and familiar navigation. | Available: searchable local library with a modern browser layout. | Partial: session-scoped character list. |
| Create a character | Established. | Available: create a local first-level character with basic profile fields. | Partial: create a temporary character workspace with the initial profile fields. |
| Open existing `.dnd5e` files | Established. | Available: local characters are discovered automatically and files can also be opened explicitly. | Available: upload a `.dnd5e` file into the current session. |
| Save or export `.dnd5e` files | Established. | Available. | Available: download from the temporary workspace. |
| Multiple characters | Established: library workflow. | Available: open characters are organized in tabs. | Partial: several session characters can be stored, with one active workspace at a time. |
| Name search | Established. | Available: fast local name filtering. | Partial: session list browsing is available; desktop-style library search is not yet the focus. |
| Groups | Established: groups, edits, and group-aware organization. | Available: group creation, edits, display, and group-name search. | Partial: imported group metadata is visible, but group management is not fully wired into browser creation and editing. |
| Favorites | Established. | Partial: favorite metadata is read and surfaced by the character browser model, but the modern UI does not yet expose the full legacy workflow. | Not yet. |
| PDF-to-character import | Not yet. | Experimental: PDF import workflow is available for supported character PDFs. | Not yet. |

## Content And Compendium

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Aurora XML content | Established: reads the legacy XML model directly. | Available: XML is ingested into a SQLite cache and reconstructed for the builder engine. | Experimental: temporary uploaded XML content can be indexed for the current session. |
| Source selection | Established. | Available: source groups and individual sources can be enabled or disabled per character. | Partial: source toggles are exposed in the browser workspace. |
| Content updater | Established: legacy index and bundle update workflow. | Available: content update checks, downloads, cache sync, and reload controls. | Not yet: hosted baseline content is managed by the deployment. |
| Compendium search | Established: legacy criteria-based search. | Available: SQLite-backed search with category-specific filters and rich details. | Partial: read-only merged baseline and session catalog with lightweight filters. |
| Spell details | Established. | Available: action, range, components, duration, level, school, class availability, and description are surfaced. | Partial: catalog-level browsing is available; rich desktop detail parity is still growing. |
| Session content uploads | Not applicable. | Not applicable: local custom content is persistent XML. | Experimental: `.xml`, `.zip`, and `.dnd5e` uploads are temporary and private to the anonymous session. |

## Character Building And Management

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Race, class, background, and other build selections | Established. | Available: unresolved build choices can be reviewed and changed. | Partial: browser build pages expose unresolved selection groups, search, picks, and changes. |
| Level advancement | Established. | Available: level up, level down, and multiclass addition are available from the session workspace. | Partial: core build selections are editable, but the desktop advancement experience is not yet reproduced in full. |
| Multiclassing | Established. | Available. | Partial: underlying character engine support is present; browser workflow depth still needs parity work. |
| Optional rules | Established. | Available: feats, multiclassing, custom origin, languages, and proficiencies can be configured. | Partial: source and narrative editing exist, but the full modern desktop options panel is not reproduced. |
| Narrative and identity details | Established. | Available. | Partial: focused narrative and identity fields are editable in the browser workspace. |
| Portrait and symbol galleries | Established. | Partial: the modern interface does not yet reproduce the complete legacy gallery workflow. | Not yet. |
| Companion management | Established legacy workflow. | Partial: companion content is represented, with more dedicated workflow polish still useful. | Not yet. |

## Equipment And Inventory

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Inventory list | Established. | Available. | Partial: focused inventory list is editable in the browser workspace. |
| Add, remove, and change quantity | Established. | Available. | Available. |
| Equip armor, main hand, and off hand | Established. | Available. | Available. |
| Attunement | Established. | Available. | Available. |
| Currency | Established. | Available. | Available. |
| Item notes and customization | Established: includes legacy item management details. | Available: item edit workflow, notes, and treasure or quest notes. | Partial: notes and core item state can be changed. |
| Starting equipment | Established. | Available: dedicated starting equipment workflow. | Not yet. |
| Extract equipment packs | Established. | Available: packs such as an explorer's pack can be replaced by their component items. | Not yet. |
| Containers and inventory storage | Established. | Not yet: the modern inventory is intentionally simpler today. | Not yet. |

## Spellcasting

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Known spells | Established. | Available. | Partial: browser known-spell picker is available. |
| Prepared spells | Established. | Available: prepared, granted, and always-prepared states are represented. | Partial: prepared toggles are available. |
| Spell slots and usage | Established. | Available: slot tracking is integrated into the session workspace. | Partial: slot usage can be updated in the browser workspace. |
| Multiple spellcasting sources | Established. | Available. | Partial: engine support is present, but the browser workflow is narrower. |
| Expanded spell lists and profile extensions | Established: Aurora XML `<spellcasting extend="true">` behavior. | Available: SQLite reconstruction reads profile extension data so lists such as Fiend Warlock flow through the same downstream logic. | Partial: shared engine behavior is available where the browser workspace reaches it; broader parity remains under test. |
| Spell filtering and details | Established. | Available: searchable spell picker and rich compendium details. | Partial: focused picker and details are available. |

## Live Session Support

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Dedicated live-session workspace | Not yet: the legacy client centers the sheet and builder workflows. | Available: designed for use during play. | Not yet. |
| Hit points and temporary hit points | Sheet-driven legacy workflow. | Available. | Not yet. |
| Inspiration, exhaustion, death saves, and conditions | Sheet-driven legacy workflow. | Available. | Not yet. |
| Short and long rests | No dedicated Reflections-style session action. | Available. | Not yet. |
| Custom resources | No dedicated Reflections-style session tracker. | Available. | Not yet. |
| Session notes | Character notes remain available through legacy workflows. | Available. | Partial: narrative editing exists, but not a dedicated live-session page. |

## Sheets, Import, And Export

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Character sheet preview | Established. | Available on supported desktop targets. | Not yet. |
| Full sheet PDF export | Established. | Available: sheet settings include fillable form, formatting, page, and card options. | Not yet. |
| Lightweight summary PDF | Not applicable. | Full sheet export is preferred. | Available: intended as a useful temporary-workspace summary, not desktop sheet parity. |
| Feature, spell, item, and attack cards | Established. | Available as configurable sheet export options. | Not yet. |
| Background and equipment sheet pages | Established. | Available as configurable sheet export options. | Not yet. |

## Settings, Updates, And Diagnostics

| Feature | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| Application update checks | Partial: legacy update and release information workflow. | Available for Velopack-installed Windows builds; portable builds can still check but do not have the same install path. | Not applicable: deployments update the hosted application. |
| Content update controls | Established. | Available: settings expose content checks, downloads, synchronization, and reload actions. | Not applicable to the anonymous session model. |
| Developer diagnostics | Established legacy tooling. | Available: Developer Mode enables the in-app console and diagnostic surface. | Server and browser diagnostics remain deployment-oriented. |
| Themes | Established: legacy theme and accent controls. | Partial: modern light-mode preference exists; broader visual customization is intentionally narrower. | Partial: follows the browser client styling. |

## Architectural Context For Contributors

| Concern | Aurora.Lights | Aurora: Reflections | Aurora.Web |
| --- | --- | --- | --- |
| UI framework | WPF | .NET MAUI Blazor Hybrid | Blazor web application |
| Shared rule engine | Uses the legacy builder engine and XML model. | Uses the same underlying builder engine through a SQLite-backed reconstruction layer. | Uses the shared engine services inside temporary server-side workspaces. |
| Best fit for small legacy fixes | Strong fit: the familiar desktop workflow remains visible and editable. | Good fit when the behavior belongs to the modern desktop client or shared reconstruction layer. | Good fit for browser-hosting, temporary workspace, and online-builder work. |
| Important boundary | Some first-party behavior still lives in referenced assemblies such as `Builder.Data.dll`. | SQLite reconstruction and modern UI integration add another layer that needs parity tests. | Multi-user hosting requires careful isolation around legacy singleton engine state. |

## Reading The Matrix

This is not a scorecard. The three clients are related evolutions with different
jobs:

- Use **Aurora.Lights** when preserving or extending the familiar legacy
  experience is the priority.
- Use **Aurora: Reflections** when you want the actively developed modern desktop
  client and its session-friendly workflow.
- Use **Aurora.Web** when you want to help move the project toward a hosted
  builder without pretending that browser parity is already complete.

When adding a feature, update this comparison alongside the implementation. If a
workflow is only partly shared, describe the working slice and the missing depth
instead of upgrading the row to `Available` prematurely.
