# Full Legacy Source Restoration

## Objective

Make the repository the authoritative, browsable implementation of Aurora's
first-party production code. A contributor should be able to understand,
modify, build, and test Aurora without decompiling its runtime dependencies.

## Scope

The production restoration covers:

1. `Builder.Core.dll`
2. `Builder.Data.dll`
3. `Aurora.Documents.dll`
4. `Aurora.Presentation.dll`
5. removal of those binaries from normal production builds after their source
   replacements pass compatibility gates

Aurora Studio is intentionally excluded. Its binaries are not an authority for
runtime behavior and will be revisited only if AuroraXMLHelper develops into a
content creation and repair suite.

The maintainer has confirmed authorization from the rights holders to reverse
engineer and modify the legacy Aurora project. Supporting correspondence may be
retained privately; it does not need to be committed with the source.

## Restoration Authorities

When sources disagree or behavior is unclear, use this order:

1. production Aurora binaries
2. observable behavior in the production legacy client
3. restored legacy WPF source
4. representative Aurora XML and `.dnd5e` fixtures
5. existing compatibility and parity tests

Aurora Studio is not part of this authority chain.

## Working Rules

- Restore one assembly at a time.
- Preserve assembly names, namespaces, public type identity, member behavior,
  exception behavior, and embedded resource names during the initial port.
- Do not combine mechanical source restoration with redesign or feature work.
- Do not create parallel look-alike domain models as a substitute for restoring
  the canonical implementation.
- Keep the original binary as a test oracle until its source replacement and
  downstream consumers pass the agreed compatibility gates.
- Once an assembly is restored, production projects consume its source project;
  the oracle binary must not remain a production reference.

## Compatibility Gates

Each restored assembly should pass:

1. **Binary inventory:** hash, version, MVID, target framework, exported-type
   count, and embedded-resource hashes are recorded.
2. **API parity:** public types, inheritance, interfaces, constructors,
   properties, fields, events, methods, constants, and enum values match.
3. **Behavior parity:** focused characterization tests cover defaults, state
   changes, events, exceptions, parsing, and serialization as applicable.
4. **Corpus parity:** the legacy and restored implementations produce equivalent
   normalized results for representative XML and `.dnd5e` fixtures.
5. **Consumer validation:** affected shared/client projects build and their
   focused tests pass.

Legacy and restored assemblies with the same identity should be compared in
separate processes so static state and singleton services cannot contaminate
the result.

## Planned Sequence

| Phase | Status |
| --- | --- |
| Record production binary baseline | Complete |
| Restore `Builder.Core` | Source-owned; API and focused compatibility checks pass |
| Restore `Builder.Data` | Source-owned; API, differential behavior, corpus, and consumer checks pass |
| Restore `Aurora.Documents` | Source-owned; API, differential behavior, embedded-resource, and consumer checks pass |
| Restore `Aurora.Presentation` | Source-owned; API, differential WPF behavior, resource-path, and consumer checks pass |
| Remove first-party oracle binaries from production paths | Complete; retained only under `tests/LegacyOracles` |

## Test-Only Oracle Isolation

The original first-party production assemblies now live in
`tests/LegacyOracles`. They are immutable compatibility fixtures used only by
the inventory and differential parity tools. Production projects reference the
restored source projects and must never reference, copy, or load files from the
oracle directory.

The remaining binaries under `lib` are third-party dependencies, not hidden
first-party Aurora implementations.

## Restoration Tools

- `tools/legacy-restoration/Get-ProductionBinaryManifest.ps1` regenerates the
  production binary inventory written to `production-binaries.json`.
- `tests/LegacyOracles` contains the original first-party assemblies used by
  the inventory and differential checks.
- `tools/AssemblyApi` emits a deterministic normalized API surface for an
  assembly.
- `tools/legacy-restoration/Compare-RestoredAssemblyApi.ps1` compares an oracle
  binary with a restored assembly in separate processes.
- `tools/legacy-restoration/Compare-BuilderDataBehavior.ps1` runs the focused
  Builder.Data compatibility suite once with the restored assembly and once
  with the production oracle in an isolated temporary test output.
- `tools/legacy-restoration/Compare-AuroraDocumentsBehavior.ps1` runs the
  focused Aurora.Documents compatibility suite against both implementations,
  including the embedded PDF templates and live form-field writing.
- `tools/WpfAssemblyApi` hosts the normalized API formatter in a Windows
  Desktop runtime so legacy and restored WPF assemblies can be inspected.
- `tools/legacy-restoration/Compare-RestoredWpfAssemblyApi.ps1` compares WPF
  API surfaces in separate processes.
- `tools/legacy-restoration/Compare-AuroraPresentationBehavior.ps1` runs the
  focused Aurora.Presentation suite against the restored assembly and the
  production oracle, including dependency properties, triggers, BAML paths,
  and live resource-dictionary loading.

Example:

```powershell
.\tools\legacy-restoration\Compare-RestoredAssemblyApi.ps1 `
  -OracleAssembly .\tests\LegacyOracles\Builder.Core.dll `
  -RestoredAssembly .\Builder.Core\bin\Debug\net10.0\Builder.Core.dll
```

`Builder.Data` API parity is compared from a Release build because modern
Roslyn Debug builds add `DebuggerStepThrough` to async methods. Those five
compiler-generated debugger attributes are absent from the production Release
binary; the Release surfaces match across all normalized API lines.

```powershell
dotnet build .\Builder.Data\Builder.Data.csproj -c Release

.\tools\legacy-restoration\Compare-RestoredAssemblyApi.ps1 `
  -Configuration Release `
  -OracleAssembly .\tests\LegacyOracles\Builder.Data.dll `
  -RestoredAssembly .\Builder.Data\bin\Release\net10.0\Builder.Data.dll

.\tools\legacy-restoration\Compare-BuilderDataBehavior.ps1
```

`Aurora.Documents` has the same normalized API surface in both Debug and
Release builds. Its differential suite also verifies the original resource
names, sizes, hashes, model defaults, export routing, notes conversion, and PDF
field mappings.

```powershell
.\tools\legacy-restoration\Compare-RestoredAssemblyApi.ps1 `
  -OracleAssembly .\tests\LegacyOracles\Aurora.Documents.dll `
  -RestoredAssembly .\Aurora.Documents\bin\Debug\net10.0\Aurora.Documents.dll

.\tools\legacy-restoration\Compare-AuroraDocumentsBehavior.ps1
```

`Aurora.Presentation` retains all 25 legacy BAML resource paths while keeping
their recovered XAML as contributor-editable source. The modern WPF compiler
regenerates the BAML bytes, so parity is checked through resource names, paths,
representative values, merged dictionaries, and successful pack-URI loading.

```powershell
.\tools\legacy-restoration\Compare-RestoredWpfAssemblyApi.ps1 `
  -OracleAssembly .\tests\LegacyOracles\Aurora.Presentation.dll `
  -RestoredAssembly .\Aurora.Presentation\bin\Debug\net10.0-windows\Aurora.Presentation.dll

.\tools\legacy-restoration\Compare-AuroraPresentationBehavior.ps1
```
