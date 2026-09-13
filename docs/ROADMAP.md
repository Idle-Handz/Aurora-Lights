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

## Content / Builder Trust

Current focus:

- make SQLite-backed content reconstruction explainable enough to trust in the
  builder
- connect content health findings to concrete build symptoms, such as empty
  picker rows, missing grants, ambiguous selection rows, and broken starting
  equipment
- prefer small fixture-backed parity checks for high-impact element families
  before broad loader rewrites
- preserve stable choice identity across builder rows and `.dnd5e` save/load
  so same-label choices do not drift between app sessions
- adopt Aurora ID alone as canonical element identity after the content cleanup, silently
  ignore harmless identical declarations, and preserve distinct references through
  selection/save/load; see [the identity plan](element-identity-feasibility.md)
- allow `user/local` content to be cached in SQLite as a separate override layer,
  preserving the authoritative base and avoiding full rebuilds for local edits
- persist explicit correction metadata in the local artifact and database; allow
  unmodified companion elements to follow authoritative updates while marked
  corrections remain pinned until direct review or verified published-match
  acceptance; see [the override policy](local-correction-policy.md)
- capture authoritative download evidence and automatically accept complete
  matching corrections/groups after successful import; disk-only matches and
  partial/differing upstream repairs remain protected for review
- automatically retire a local file and its override cache once no marked
  corrections remain and its full effective content matches an imported
  authoritative update; retain any still-unique local content
- first correction lifecycle implementation: embedded v1 metadata, a separate
  SQLite mirror of originals and effective content, protected refreshes, explicit
  review API, and recoverable retirement; see [implementation notes](local-correction-implementation.md)
- six known hotfix files explicitly annotated from archived originals, with ten
  protected operations and file-scoped runtime suppression; see
  [the deployment record](hotfix-metadata-annotation-2026-09-13.md)
- keep [the Translator handoff](../../5eApiTranslator/docs/aurora-translator-data-handoff.md) current as each
  data feature lands; [remaining UI/migration estimates](content-migration-estimates.md)
  separate implementation effort from library extraction and platform validation
- neutral review/provenance records and read-only canonical classification now
  exist in `Builder.Data`; production adapters and reference scanning remain pending
- resolve content in app/CLI preparation before the SQLite writer receives it;
  reuse AuroraXMLHelper diagnostics and repair previews through adapters, preserving
  its manual suggestions without automatically applying ID-regeneration placeholders;
  see [the preparation contract](content-preparation-contract.md)

Next slice:

- add builder-facing diagnostics when a selection row cannot produce options
- classify common broken XML shapes and surface repair-oriented hints, starting
  with missing target element types, unmatched support tags, and malformed
  list-style selections
- add focused parity checks for class/race/background choices, spellcasting
  selects, grants, and starting equipment reconstruction
- begin a "content repair suggestion" layer that can propose Aurora element XML
  fixes without mutating user content automatically
- for users with Advanced options enabled, surface ID conflicts detected during
  database sync in a compact diff/resolution window inside the builder; show source
  provenance, complete definition differences, and affected references before
  applying a repair as a persistent local hotfix
- consider reusing Constellations/Aurora XMLHelper/Aurora Studio conflict-resolution
  logic for that window; keep larger authoring workflows in those tools while
  supporting small repairs directly in the builder, and flag local corrections
  for retirement once verified fetched upstream content incorporates and accepts
  the complete fix, retaining review for unverified or differing outcomes

## Shared Importer Migration (Later)

- After resolving selected-element identity and definition-conflict behavior, extract
  the authoritative AuroraTranslator importer into a versioned shared library.
- Have Translator's CLI and Lights use the same implementation for SQLite creation,
  refresh, migrations, and required package/source database maintenance; retire the
  copied writer only after its callers and behavior have been migrated.
- Validate import and SQLite loading on Android early. Normal loading must remain
  SQLite-backed on supported platforms; runtime XML is a recovery path, not the
  only platform option. Desktop CLI builds can remain optional wrappers.
- Preserve progress, cancellation, multiple content directories, and platform-safe
  file access, and establish a separate reader compatibility contract and publishing
  check. Keep source availability, display ordering, and definition-conflict policy
  distinct rather than inheriting current package precedence implicitly.

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

## Character Journal / Quest Tracking (Later)

- add structured, date-stamped session-log entries instead of requiring players
  to maintain chronology inside the two existing free-form note fields
- add quest records with a title, status, objectives, important NPCs and
  locations, rewards, and free-form notes
- allow log entries to be added, edited, removed, searched, and optionally
  linked to one or more quests while retaining their original session dates
- keep the existing `<quest>` character-file field reserved for
  `Inventory.QuestItems`; store journal and quest records in a separate,
  versioned model
- design persistence, migration, and export in the shared layer first so
  Reflections and future Web support round-trip the same data without dropping
  legacy notes

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
- ability score assignment is available in the browser Build page across all five
  methods (Manual, Roll 4d6, Roll 3d6, Standard Array, Point Buy) with ASI bonus
  display and modifier row
- level up, level down, and the HP method toggle (average or rolled) are available
  from the browser Build page advancement strip

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

**Phase 1 candidate — Aurora XML Studio (content creation)**

Phase 0 covers content *consumption*: upload existing XML, build a character,
download your work. A natural Phase 1 would add content *authoring* without
changing the storage model.

The AuroraXMLHelper project (`repos/AuroraXMLHelper`) already encodes the full
Aurora element schema in `aurora-xml-shape.js` and handles PDF→XML conversion
via deterministic parsing. The Studio concept extends that by adding form-based
authoring — create elements from scratch with no PDF, export a valid Aurora XML
file, store it locally, re-upload via the existing import workflow.

**Development path**: prototype the form-based authoring inside AuroraXMLHelper
first, where the schema knowledge already lives and iteration is fast. Once
proven, bring it into Aurora.Web and/or Aurora.Legacy — either as an embedded
feature or by linking out to a rebranded standalone tool. The delivery mechanism
(integrate vs. link, rebrand or not) is TBD once the authoring workflow is
working end-to-end.

Suggested first-pass element types: Races, Classes, Subclasses, Backgrounds,
Feats, Spells, Items, and Magic Items.

**Later phases (unscoped)**

- optional accounts for persistent user libraries
- richer upload/import ergonomics
- stronger compendium/search indexing if in-memory indexing proves insufficient
- browser-safe preferences and session restore
- optional external authentication providers if and when persistent user data becomes worthwhile
