# Aurora.Logic

`Aurora.Logic` is the shared application core for Aurora-Lights.

## Purpose

This project is the compatibility and migration anchor between clients.

It is intended to contain:

- rules and progression logic
- character/domain models
- content loading and indexing
- sheet generation
- shared services and contexts
- app settings and neutral infrastructure

## Current Direction

The main architectural goal is to keep this project as client-neutral as possible so it can be reused by:

- the legacy WPF app
- the MAUI app
- the `Aurora.Web` host

Recent cleanup in this project includes:

- removal of the WPF presentation dependency from this shared layer
- removal of shared `System.Drawing` usage
- replacement of direct `Process.Start(...)` usage with a launcher abstraction
- path handling fixes so shared content loading no longer assumes Windows path separators

## Source Ownership And Dependencies

This project and its first-party dependencies (`Builder.Core`, `Builder.Data`,
and `Aurora.Documents`) are maintained and built from source in this repository.
The original Aurora binaries are test-only compatibility fixtures under
`tests/LegacyOracles`, not build or runtime dependencies of this project.
Third-party references, including DynamicExpresso, iTextSharp, and NuGet
packages, remain declared in `Aurora.Logic.csproj`.

Some source originated through reconstruction of the legacy application and
still preserves WPF-era assumptions. That history describes the source's
provenance, not a dependency on an external legacy implementation. Those seams
are being moved behind shared abstractions incrementally. See
[source ownership and legacy compatibility](../docs/LEGACY_RESTORATION.md).

## Expected Responsibility Split

- shared runtime/state repair belongs here when both clients benefit from a canonical behavior
- host-only conveniences belong in the client projects
- temporary web-session upload handling may eventually live behind interfaces here, but not as browser-specific code
