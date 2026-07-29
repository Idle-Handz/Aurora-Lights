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

## Restored Legacy Source

The repository is source-transparent for the four first-party assemblies that
were formerly production binary boundaries:

- `Builder.Core` contains shared events, logging, commands, and observable
  infrastructure.
- `Builder.Data` contains the foundational Aurora element model, XML parsing,
  grants, selections, statistics, content files, and update primitives.
- `Aurora.Documents` contains the PDF and character-sheet writing
  infrastructure.
- `Aurora.Presentation` contains the remaining WPF controls, event triggers,
  styles, themes, and control templates.

Production projects reference these editable source projects. The original
production assemblies are retained only under `tests/LegacyOracles` so the
restored implementations can be checked for API and behavior parity. They must
not be referenced or copied by production builds. The `lib` directory contains
third-party dependencies still used by the applications.

Contributions should change the restored source and add source-native tests.
The differential oracle checks remain useful regression gates where exact
legacy compatibility matters.

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
