# Aurora.Presentation

This project is the source restoration of the production
`lib/Aurora.Presentation.dll` WPF assembly.

The initial source intentionally preserves the original namespaces, public API,
assembly version, dependency-property behavior, event triggers, resource
dictionary paths, styles, themes, and control templates. Compatibility fixes
and cleanup should be made separately from the mechanical restoration so
behavior changes remain reviewable.

The original binary remains in `lib` as a restoration oracle while compatibility
gates are exercised. Production projects consume this source project.
