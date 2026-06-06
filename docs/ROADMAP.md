# Roadmap

## Near-Term Priorities

1. Stabilize the shared layer as a true multi-client core.
2. Keep the MAUI app moving forward as the primary modern client.
3. Preserve WPF compatibility without treating WPF as the main innovation surface.
4. Start a Phase 0 `Aurora.Web` host with no long-term user-content storage.

## Shared Layer

Current focus:

- continue removing platform assumptions from `Aurora.Logic`
- keep launch/dialog/file seams behind abstractions
- make shared content loading and file-path behavior safe across Windows and macOS
- add tests around save/load, build choices, equipment state, and generated output over time

## MAUI

Current focus:

- expand the modern shell and workflow coverage
- improve desktop polish
- validate Mac Catalyst behavior on actual macOS hardware
- keep MAUI-only features, such as sessions, where they provide clear value for migration

Next slice:

- Add sanitized `.dnd5e` character fixtures for recurring Reflections testing. Characters grouped under `Old Characters` are acceptable source material; active characters should be approximated or sanitized before committing.
- Cover fragile fixture scenarios: level-1 build choices, race/background ASI options, prepared casters with always-prepared spells, multiclassing, portraits/groups, and Legacy-edited files.
- Run a Legacy/Reflections parity pass over similar build tasks before moving more logic: selection rules, advancement timelines, spell preparation, ASI surfacing, leveling, and character save/reload behavior.
- Inventory `Aurora.App.Services.BuildService` responsibilities and identify which pure, non-UI pieces could move closer to `Aurora.Logic` or a shared/testable service. Keep MAUI dialogs, file pickers, page state, and app cache behavior in `.App`.
- Prefer fixture-backed parity tests before refactoring shared build logic, so behavior stays aligned with the original WPF expectations unless a deliberate fix is being made.

## WPF

Current position:

- continue supporting it as a parallel client
- preserve cross-compatibility for character/content data
- avoid large new feature investments unless they are compatibility-critical

## Aurora.Web Phase 0

Target model:

- hosted `core + SRD` baseline content only
- user-supplied XML content handled privately and ephemerally
- no required accounts
- no long-term persistence of uploaded non-SRD content
- export/download of `.dnd5e` files and generated PDFs

Current status:

- `Aurora.Web` now exists as an ASP.NET Core Blazor host in the solution
- anonymous session workspaces can accept `.xml`, `.zip`, and `.dnd5e` uploads
- uploaded XML is indexed into lightweight in-memory element summaries
- a first merged compendium page now combines embedded baseline content with the current session overlay
- imported characters can now be opened into the current browser session
- new temporary characters can now be created and downloaded back out as `.dnd5e`
- a lightweight PDF summary export is now available for the active session character
- stale temporary workspaces are cleaned up automatically
- `Aurora.Components` now provides shared UI fragments used by the MAUI and web
  clients
- first-pass Build, Manage, Equipment, and Magic editing workflows are available
  in the browser workspace

Planned implementation shape:

1. Add a web host project.
2. Continue moving reusable Razor UI into `Aurora.Components` so the MAUI and
   web hosts can share the same source.
3. Build a session-scoped content overlay service.
4. Parse uploaded XML once per session and build in-memory indexes for compendium/equipment/spell lookups.
5. Add temporary workspace cleanup/expiration.
6. Introduce a download-focused character/PDF flow instead of any server persistence.
7. Deepen the first browser editing workflows toward desktop parity.

## Web Phase 0 Non-Goals

- public hosting of non-SRD content packs
- cross-device user libraries
- account system or user database
- long-term server-side character storage
- whole-desktop-folder mirroring as the initial upload model

## Likely Later Web Phases

- optional accounts for persistent user libraries
- richer upload/import ergonomics
- stronger compendium/search indexing if in-memory indexing proves insufficient
- browser-safe preferences and session restore
- optional external authentication providers if and when persistent user data becomes worthwhile
