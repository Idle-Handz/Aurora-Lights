# Contributing To Aurora Lights

Thank you for taking an interest in Aurora Lights. This repository is an
incremental modernization of the legacy Aurora character builder rather than a
single-client rewrite. There are several useful ways to contribute, including
small improvements to the familiar desktop application.

## Choose A Project

- `Aurora.Lights`
  The legacy-facing WPF desktop client. This is the best starting point for
  improving the familiar Aurora interface, fixing legacy UI bugs, or restoring
  desktop behavior.

- `Aurora.Logic`
  The shared compatibility layer used by multiple clients. It contains
  character loading, progression orchestration, content handling, inventory,
  sheet generation, settings, and other shared behavior.

- `Aurora.App`
  The MAUI Blazor Hybrid host for **Aurora: Reflections**. This is the primary
  current development focus and the best home for improvements to the modern
  desktop and mobile experience.

- `Aurora.Components`
  Shared Razor components used by modern clients.

- `Aurora.Importer`
  The SQLite content importer used by the modern content pipeline.

- `Aurora.Web`
  An early browser-hosted Aurora experiment and first step toward a fully
  online builder.

You do not need to work on Reflections or the web client to make a valuable
contribution. Legacy-oriented fixes and modest features remain welcome,
especially when they improve behavior shared by more than one client.

For a workflow-by-workflow view of the clients, see the
[client feature comparison](docs/CLIENT_FEATURE_COMPARISON.md).

## How Editable Is The Legacy Client?

`Aurora.Lights` is a real, buildable WPF application on .NET 10. Its views,
XAML, controls, dialogs, commands, and many view models are available as source.
It consumes the editable `Aurora.Logic` project for a substantial portion of
its shared behavior.

This makes the repository suitable for work such as:

- fixing desktop UI bugs
- improving dialogs, views, commands, and view models
- restoring or extending modest legacy features
- troubleshooting character-file loading and compatibility issues
- improving inventory, sheet-generation, and content-management workflows
- repairing behavior in the shared orchestration layer
- incrementally replacing historical assumptions with clearer abstractions

Some files were reconstructed from the legacy application with a decompiler.
They intentionally preserve older naming, namespaces, and structure where that
helps maintain compatibility. Prefer focused changes over broad rewrites unless
a larger refactor is necessary and well tested.

## Known Binary Boundaries

The repository is not yet a completely source-transparent reconstruction.
Several legacy assemblies remain checked into `lib` and are referenced by the
editable projects.

### `Builder.Data.dll`

This is the most significant remaining boundary. It provides foundational
Aurora data types and behaviors, including:

- element models for spells, races, classes, items, feats, and related content
- XML element parsers
- grant, selection, and statistic rule primitives
- element collections, setters, and helper types
- legacy content-file and update primitives

Ordinary feature work can often be completed in the source projects around
this boundary. Changes to parser semantics, the deepest rule representation, or
fundamentally new element types may require additional reconstruction work.

### Other First-Party Assemblies

- `Builder.Core.dll`
  Provides compact shared infrastructure such as events, logging, relay
  commands, and observable-object support.

- `Aurora.Documents.dll`
  Provides legacy PDF and character-sheet writing infrastructure.

- `Aurora.Presentation.dll`
  Provides a small remaining set of WPF controls and keyboard event triggers.

The `lib` directory also contains third-party dependencies used by the legacy
desktop application.

These binary boundaries are known technical debt, not a signal that
legacy-oriented contributions are unwelcome. Reconstructing or replacing them
incrementally is itself a useful contribution when done carefully.

## Where Should A Change Go?

| Change | Likely Project |
| --- | --- |
| Familiar WPF interface, dialog, or desktop command | `Aurora.Lights` |
| Shared character loading, rules orchestration, inventory, or compatibility behavior | `Aurora.Logic` |
| Reflections interface or MAUI platform integration | `Aurora.App` |
| Reusable Razor UI for modern clients | `Aurora.Components` |
| SQLite content reconstruction or importer fidelity | `Aurora.Importer` |
| Browser-hosted workflow | `Aurora.Web` |

When possible, place canonical behavior in `Aurora.Logic` so fixes benefit more
than one client. Keep platform-specific integrations in their host projects.

## Build The Projects

The repository uses .NET 10. On Windows, useful project-level build commands
include:

```powershell
dotnet build .\Aurora.Lights\Aurora.Lights.csproj -v minimal
dotnet build .\Aurora.Logic\Aurora.Logic.csproj -v minimal
dotnet build .\Aurora.App\Aurora.App.csproj -v minimal -f net10.0-windows10.0.19041.0
dotnet build .\Aurora.Web\Aurora.Web.csproj -v minimal
dotnet test .\Aurora.Tests\Aurora.Tests.csproj -v minimal
```

`Aurora.Lights` is a Windows WPF application. `Aurora.Logic`, the importer, and
the modern-client projects are intended to support broader reuse. A
solution-wide build also attempts Reflections' Android and Mac Catalyst targets,
so use the explicit Windows framework above unless the additional platform
workloads are installed.

## Work Safely With Legacy Data

Aurora: Reflections and the legacy desktop application can share the same
`Documents\5e Character Builder` directory. When testing either client:

- back up your character directory
- avoid editing the same `.dnd5e` file in both applications at the same time
- avoid running a legacy content update while Reflections is syncing or
  reloading content
- remember that `custom\aurora-elements.sqlite` is a rebuildable Reflections
  cache; the XML files remain the source of truth

## Submit A Focused Change

Before opening a pull request:

1. Describe the user-visible problem or improvement.
2. Keep the implementation scoped to the relevant project where practical.
3. Build the projects affected by the change.
4. Add or update a focused regression test when the behavior can be exercised
   automatically.
5. Include manual verification notes for UI changes.

If a change runs into one of the remaining binary boundaries, describe the
limitation clearly. A partial investigation that maps the next reconstruction
step can still be valuable.
