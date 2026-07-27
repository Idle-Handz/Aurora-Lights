# Aurora.Documents

This project is the source restoration of the production
`lib/Aurora.Documents.dll` assembly.

The initial source intentionally preserves the original namespaces, public API,
assembly version, PDF templates, export models, and document-writing behavior.
Compatibility fixes and cleanup should be made separately from the mechanical
restoration so behavior changes remain reviewable.

The original binary remains in `lib` as a restoration oracle while downstream
first-party assemblies are migrated. Production projects consume this source
project.
