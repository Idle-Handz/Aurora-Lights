# Aurora Reflections

`Aurora.Reflections` is the MAUI Blazor Hybrid host for **Aurora Reflections**, the
primary modern client in the Aurora Lights project.

## Purpose

This project provides the application shell for the modern Aurora experience.
It reuses rules and content logic from `Aurora.Logic`, shared Razor components
from `Aurora.Components`, and MAUI-specific services for native platform seams.

## What Lives Here

- MAUI host and bootstrap code
- Blazor routing and layouts
- page components for character building, equipment, shop, compendium, sheet,
  preferences, updates, and session flows
- MAUI-specific launcher, dialog, preference, file-picker, PDF-preview, and
  update services
- desktop and mobile UX features that do not need to be shared with the WPF
  client

## Current Status

The project targets:

- `net10.0-windows10.0.19041.0`
- `net10.0-maccatalyst`
- `net10.0-android`

Windows x64 and Android ARM64 are the stable 1.0 targets. RC5 has passed native
package acceptance on both, including installed Android PDF Save, Share, and
Open actions. Windows ARM64 and Mac Catalyst remain experimental; Web is deferred.

The public RC5 release is an unsigned Windows x64 prerelease with matching
source. Android was validated separately and was not uploaded to that release.
Stable package preparation is pending SignPath approval and integration. See
the [current release status](../docs/v1.0-release-status.md) and the
[installation and backup guidance](../README.md#install-aurora-reflections).

## Relationship To Other Projects

- depends on `Aurora.Logic` for shared domain behavior
- consumes `Aurora.Components` for reusable Razor UI
- should avoid owning business rules that need to stay compatible with WPF
- may own MAUI-only behavior where it provides a cleaner migration path to the
  modern client

Examples of MAUI-specific concerns include:

- native shell and window behavior
- session-tracker UX
- local preferences
- app and content update UX
- desktop PDF preview and Android PDF Save, Share, and Open actions

## Future Direction

`Aurora.Web` should continue reusing `Aurora.Components` where practical while
remaining independent from the MAUI host assembly. Shared domain behavior
belongs in `Aurora.Logic`; host-specific integrations should remain in their
respective application projects.
