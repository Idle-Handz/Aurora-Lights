# App workspace layout regressions

Run `npm run test:ui-layout` from the repository root. Prerequisites are the .NET 10 SDK,
`npm ci`, and `npx playwright install chromium`. No MAUI workload, content library,
device, account, or running application is needed.

The setup builds a small bUnit renderer for the production Shop, Equipment, Magic, and
Character Library components. Chromium loads their generated HTML and compiler-scoped CSS with the
App stylesheet, MudBlazor stylesheet, and the actual `MainLayout` Body wrapper.
The surrounding shell is a minimal fixture for the toolbar, character tabs, optional
cloud banner, status bar, and mobile navigation. If that shell changes, update the
fixture alongside it. These are browser layout and interaction-guard checks, not a
substitute for native MAUI, business-event, or device smoke tests.

Eight checks cover independent result/description scrolling and visible desktop
checkout, reachable checkout on small portrait/landscape screens, long and unbroken
inventory names at mobile breakpoints, library header/actions and long character/group
names at 200% text in a narrow viewport, and the wrapper's inert focus/click boundary.
Assertions measure geometry and hit testing rather than pixel snapshots or CSS text.
Magic checks cover the All and Prepared detail cards on desktop and mobile, including
reachable attack/damage text and higher-level scaling, mouse/keyboard scrolling, and
preventing the description from collapsing into a nested scroll area.
Picker checks use real MudBlazor dialog chrome with representative long picker content
to verify corner dragging expands both panes, actions remain visible, and narrow screens
retain reachable descriptions and actions without desktop resizing.
Four additional checks load the App's actual Back helper in a small DOM host to
verify overlay stacking, non-dismissible dialogs, the mobile More backdrop, and
returning control to Android when no overlay remains. Native key dispatch and
safe-area menu placement still require device smoke tests.
The standard Tests workflow runs this suite; traces and screenshots are saved on
failure under `test-results/`.
