# Builder.Core

This project is the source restoration of the production
`lib/Builder.Core.dll` assembly.

The initial source intentionally preserves the original namespaces, public API,
assembly version, and observable behavior. Compatibility fixes and cleanup
should be made separately from the mechanical restoration so behavior changes
remain reviewable.

The original binary remains in `lib` as a restoration oracle while downstream
assemblies are migrated. Production projects consume this source project.
