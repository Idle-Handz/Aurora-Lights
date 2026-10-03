# Embedded correction metadata compatibility

Investigation date: 2026-09-13. Result: a separately namespaced metadata section
under the existing, unnamespaced `elements` root is tolerated by the tested Lights
readers and both SQLite import paths. Correction interpretation, database mirroring,
protection during upstream updates, and retirement are not implemented by this work.

Subsequent lifecycle implementation is documented in
[the implementation notes](local-correction-implementation.md); this report records
the preceding compatibility investigation.

## Documentation reviewed

- [Aurora XML Documentation.docx](<C:/Users/Ralla/Downloads/Aurora XML Documentation.docx>):
  reviewed its 426 extracted paragraphs and the embedded content examples. Its
  Fundamentals and Anatomy sections describe the `elements` root and optional
  file/update `info` section; its Child Tags section describes gameplay nodes.
  No explicit extension mechanism or guarantee for unknown/namespaced nodes was
  found. The document was treated as reference material, not operational instructions.
- [Anatomy of the Element](https://aurorabuilder.com/documentation/anatomy-of-the-element/):
  describes the four element attributes and gameplay child nodes. It does not
  establish a contract for a new file-level metadata section. Direct page fetches
  returned HTTP 429; the search tool supplied the indexed page text for review.
- [Hosting Content](https://aurorabuilder.com/documentation/hosting-content/):
  assigns update semantics to `info` and requires its `update` section. This
  supports keeping correction metadata separate from that existing block.

Documentation reveals no named-node conflict with the proposed extension, but
silence is not proof of compatibility. The conclusion below depends on code
inspection and execution against the current implementations.

## Tested placement and constraints

Illustrative structure only; field validation and final operation schema remain
implementation work:

```xml
<elements>
  <!-- Existing info and gameplay declarations remain here. -->
  <al:corrections xmlns:al="urn:aurora-lights:corrections:1" version="1">
    <al:correction key="tatsumi-heartening-breath"
                   operation="rename"
                   target-id="ID_RGTTYR_RACIAL_TRAIT_TATSUMI_RYUJIN_CLOUDSTEP"
                   replacement-id="ID_RGTTYR_RACIAL_TRAIT_TATSUMI_RYUJIN_HEARTENING_BREATH"
                   state="review-pending">
      <al:reason>Separate Heartening Breath from Cloudstep.</al:reason>
    </al:correction>
  </al:corrections>
</elements>
```

The actual rename also needs the original file/declaration fingerprint because
both old definitions claimed the same ID, and a link to the parent-grant correction.
The sample above is not sufficient to execute that repair by itself.

Required boundaries established by the investigation:

1. Keep metadata beside `info`, `element`, and `append`, outside gameplay elements.
   Both importers inspect gameplay child local names and preserve unknown blocks;
   placing metadata inside an element could contaminate its stored blocks or collide
   with recognized gameplay names.
2. Do not add a default namespace to `elements` or its gameplay descendants. A
   negative-control test demonstrated that this causes the copied importer to find
   zero elements, even though the XML remains well formed.
3. Do not introduce an `info` block containing only correction metadata. The current
   `ElementsFile` reader throws when `info/update` is missing. An existing valid
   `info` block must retain its established update information.
4. Store baseline XML as escaped text (or another explicitly encoded payload), not
   live XML descendants. `AuroraXmlCompatibilityRepair` recursively repairs all
   descendants, including an unknown metadata section. A negative-control test
   demonstrated alteration of a baseline `spellcastin` attribute; escaped text
   survived unchanged. A namespace alone does not isolate descendants from that pass.
5. Missing/unsupported metadata versions must eventually be handled conservatively
   by the correction reader. The current readers ignore this section; they do not
   validate its operations or confer protection on it.

## Code paths inspected

- `Builder.Data/Files/ElementsFile.cs`: selects direct unprefixed `element` and
  `append` children; reads the existing `info` block. `SaveContent` writes the
  supplied XML content and retained the metadata in the round-trip test.
- `Aurora.Logic/Services/Data/DataManager.cs`: direct `element` discovery for XML
  loading, with the compatibility repair pass.
- `Aurora.App/Services/RawUserXmlOverlayService.cs`: the same direct-node discovery,
  typed parser dispatch, and `ElementsFile` append discovery. Inspected directly;
  tests exercise its file reader, repair pass, and typed parsers, not the singleton
  service against the installed user directory.
- `Aurora.Importer/AuroraXmlCatalogReader.cs`: direct unnamespaced `element` selection;
  file-level correction metadata is not imported into its catalog.
- Sibling Translator `5eApiTranslator/Program.cs`: direct unnamespaced `element`
  selection. The bundled executable was additionally exercised, independently of
  the sibling source inspection.
- Aurora XMLHelper's `Test-AuroraXmlShape.ps1`: executed against all six annotated
  full-file copies. Its browser shape validator was inspected, but its editor
  save/export workflows were not executed.

## Executed checks

| Check | Result |
| --- | --- |
| Typed runtime parsing: Devout, Tatsumi, Musketball, Staff of Flowers excerpts | 4 passing cases; file info and parsed definitions unchanged |
| Copied importer: initial import, metadata addition, review-state edit, removal | 4 passing cases; all compared nonvolatile database rows unchanged |
| Repair and file save/reload | Metadata retained; nested sentinel element/append did not enter gameplay collections |
| Negative controls | Missing `info/update` rejected; default root namespace dropped discovery; live baseline subtree was mutated |
| Focused xUnit suite | 12 passing tests |
| Bundled Translator, six complete recent override files | 247 elements / 247 distinct IDs; 56 compared tables unchanged |
| Repaired grant resolution | Both Devout grants and Heartening Breath resolved to the intended target rows |
| Temporary Translator database | Integrity and foreign-key checks passed |
| Aurora XMLHelper shape validation | 6 files; 0 errors; 0 warnings |

Database comparisons exclude `database_metadata`, `import_state`, file hashes, and
package creation timestamps, since those represent import bookkeeping. Gameplay
definitions, rules, references, additional blocks, source/update fields, and
resolution caches are included. Metadata-only edits currently trigger an ordinary
file reimport; efficient correction-only database updates are future work.

The persistent fixtures are exact selected element blocks from the installed
hotfix copies, with their existing `info` blocks. Their source paths and full-file
SHA-256 fingerprints are in `Aurora.Tests/Fixtures/CorrectionMetadata/provenance.json`.
Nested sentinel declarations in the test metadata deliberately detect accidental
descendant-based discovery; they are not a proposed production payload format.

## Reproduction and evidence

```powershell
dotnet test Aurora.Tests/Aurora.Tests.csproj --no-restore --filter FullyQualifiedName~CorrectionMetadataCompatibilityTests
python tools/test-correction-metadata.py --overrides 'C:\Users\Ralla\Documents\5e Character Builder\custom\user\local\id-hotfixes-20260913' --translator 'C:\Users\Ralla\source\repos\Aurora-Lights\Aurora.App\BundledTools\AuroraTranslator\AuroraTranslator.exe'
```

The CLI probe reads original files and creates a new temporary directory for its
copies, databases, logs, and report. The recorded run is summarized in
[the CLI evidence](correction-metadata-cli-evidence.json) and
[the shape-validator evidence](correction-metadata-shape-evidence.json).

No installed XML was annotated, no production database was refreshed, and no
application or character save was modified. These are compatibility probes, not
a completed correction feature. Unmodified legacy Aurora binaries, Constellations,
Aurora Studio, and third-party editor round trips are outside the executed scope;
their preservation of this extension is not established by these tests.
