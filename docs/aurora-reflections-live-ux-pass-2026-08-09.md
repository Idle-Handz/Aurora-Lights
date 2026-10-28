# Aurora.Reflections Live UX Pass - 2026-08-09

## Scope

Reviewed the Windows desktop MAUI app live from the built `Aurora.Reflections.exe`. The pass covered the normal startup path, character selection, character loading, the primary character tabs, global Compendium, Settings, and Console.

## Flow Observed

- Startup opened to the Characters library and found 59 local characters under `Documents\5e Character Builder`.
- Character cards were readable and scannable, with portrait/initials, name, level, class, and file context visible.
- Opening a character showed an explicit loading state with progress text. The tested character, Kleeck, loaded after roughly 15-20 seconds.
- The character shell exposed the expected working areas: Overview, Build, Equipment, Session, Magic, Manage, and Sheet.
- Global navigation exposed Characters, Settings, Compendium, and Console.

## Strengths

- The desktop app has the clearest complete workflow today: choose a saved character, load it, inspect the overview, modify build/equipment/session state, inspect magic, edit identity/options, and export or save a sheet.
- The Overview page is dense but coherent. Key identity, combat stats, advancement, ability scores, and skills are available without changing screens.
- Build is much closer to the intended Aurora workflow than the current Web implementation. It exposes advancement controls, build categories, selected options, and change actions in one place.
- Equipment, Session, Magic, Manage, and Sheet each have clear local purpose and useful controls.
- Settings is product-like rather than developer-like: theme, character sheet, content, updates, and advanced tabs are understandable entry points.
- Console is discoverable and useful for diagnosing content/load issues. The warning dot on the nav gives good feedback that something needs attention.

## Issues To Revisit

- The app restored or launched into a wide desktop window that was awkward in this test environment. Window placement/restoration should avoid partially off-screen or hidden placement where possible.
- Character loading took long enough that the progress state matters. The app handles this better than Web, but the warning `loaded ... without all elements` and related missing element warnings should remain visible enough to diagnose.
- The left nav is effective at desktop width, but the two-column lower character nav can be visually dense. It works, though it is not as immediately parseable as the global nav.
- Compendium starts in an empty "Run a search" state. That is clean, but it depends on users understanding they must search before seeing content. The top search box and page search controls also overlap conceptually.
- Sheet export controls are polished, but they assume the user understands the difference between Preview, Save PDF, and Export Character File.

## Parity Notes For Aurora.Web

- Web should borrow the App's character-centered flow first: Characters -> open character -> Overview/Build/Equipment/Session/Magic/Manage/Sheet.
- Web should avoid presenting implementation status as product copy. The App rarely does this, which makes it feel more complete.
- Web import should feed the same kind of character library/open-character experience rather than only surfacing current-session uploads.
- Web Compendium should follow the App's search-first pattern or add real pagination/virtualization before exposing large result sets by default.
