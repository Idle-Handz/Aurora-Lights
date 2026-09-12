# Aurora.Presentation

This project is the maintained, project-owned WPF implementation of `Aurora.Presentation`.
Production consumers build it from local source through project references.

The initial source intentionally preserves the original namespaces, public API,
assembly version, dependency-property behavior, event triggers, resource
dictionary paths, styles, themes, and control templates. Compatibility fixes
and cleanup should be made separately from the mechanical restoration so
behavior changes remain reviewable.

The original binary remains in `tests/LegacyOracles` as a test-only restoration
oracle. Production projects consume this source project.
The oracle is required only for explicit legacy comparison checks, not normal
builds or source-native tests. Third-party WPF dependencies remain declared in
`Aurora.Presentation.csproj`. See
[source ownership and legacy compatibility](../docs/LEGACY_RESTORATION.md).
