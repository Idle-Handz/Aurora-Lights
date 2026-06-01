# Aurora: Reflections

`Aurora.App` is the MAUI Blazor Hybrid host for **Aurora: Reflections**, the
primary modern client in the Aurora Lights project.

## Purpose

This project provides the application shell for the modern Aurora experience.
It reuses rules and content logic from `Aurora.Logic`, shared Razor components
from `Aurora.Components`, and MAUI-specific services for native platform seams.

## What Lives Here

- MAUI host and bootstrap code
- Blazor routing and layouts
- page components for character building, equipment, compendium, sheet,
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

Windows remains the most exercised platform and is the recommended beta-preview
target. Mac Catalyst and Android builds are produced by CI but still need
broader runtime validation.

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
- browser-like PDF preview hosted in a MAUI window

## Future Direction

`Aurora.Web` should continue reusing `Aurora.Components` where practical while
remaining independent from the MAUI host assembly. Shared domain behavior
belongs in `Aurora.Logic`; host-specific integrations should remain in their
respective application projects.
