# Legacy Restoration Oracles

This directory contains immutable copies of the four original first-party
production assemblies used to verify the source restorations:

- `Builder.Core.dll`
- `Builder.Data.dll`
- `Aurora.Documents.dll`
- `Aurora.Presentation.dll`

These binaries are test fixtures, not runtime dependencies. Production
projects must reference the restored source projects and must not reference,
copy, or load files from this directory.

The expected hashes, versions, public type counts, and embedded resources are
recorded in
`tools/legacy-restoration/production-binaries.json`. Differential parity tools
under `tools/legacy-restoration` load these fixtures in isolated processes so
legacy static state cannot contaminate the source implementation.

Aurora Studio is intentionally excluded from this baseline.
