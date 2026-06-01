# Aurora.Web

`Aurora.Web` is the browser-hosted Phase 0 web shell for Aurora-Lights.

## Current Goal

This first implementation slice focuses on anonymous, session-scoped usage:

- embedded baseline content remains the long-term public host content
- users can upload private `.xml`, `.zip`, and `.dnd5e` files for the current session
- uploads are stored in a temporary workspace only
- uploaded XML is indexed into lightweight in-memory element summaries
- a read-only compendium view merges baseline content with the current session overlay
- imported character files can be opened into the current browser session
- new characters can be created inside the temporary session workspace
- active characters can be downloaded back out as `.dnd5e` files
- active characters can also be exported as a lightweight PDF summary
- stale workspaces are cleaned up automatically

## Current Scope

The browser client now exposes first-pass editing surfaces for:

- character creation and temporary session workspaces
- unresolved build selections
- narrative details and source toggles
- inventory, equipment slots, currency, and item state
- known spells, prepared spells, and spell-slot usage
- `.dnd5e` downloads and lightweight summary PDFs

These are useful browser workflows, but they are intentionally narrower than the
desktop applications. Uploaded files remain ephemeral and private to the current
anonymous session.

## Near-Term Next Steps

- expand the merged content service beyond the first compendium page
- reuse more shared Razor UI from `Aurora.Components`
- replace the lightweight PDF summary export with a fuller character-sheet renderer
- deepen browser editing workflows where the first-pass pages do not yet match
  the desktop clients

## Current Constraints

- the shared Aurora runtime still relies on process-wide singletons, so `Aurora.Web` currently serializes character-engine operations behind a server-side lock
- this is acceptable for an early Phase 0 proof-of-concept, but it is not the final multi-user shape
