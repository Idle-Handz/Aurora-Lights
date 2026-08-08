# Builder.Data

This project is the source restoration of the production
`tests/LegacyOracles/Builder.Data.dll` assembly.

The initial source intentionally preserves the original namespaces, public API,
assembly version, XML parsing behavior, data models, rules, and update services.
Compatibility fixes and cleanup should be made separately from the mechanical
restoration so behavior changes remain reviewable.

The original binary remains in `tests/LegacyOracles` as a test-only restoration
oracle. Production projects consume this source project.
