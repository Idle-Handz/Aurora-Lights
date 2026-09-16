# Translator integration test setup

The ordinary `dotnet test Aurora.Tests/Aurora.Tests.csproj` suite requires no
Translator executable. It runs the in-process projection tests and explicitly
reports the external writer integration as **skipped** unless
`AURORA_TEST_TRANSLATOR` is set. A skip is not evidence that database creation or
release packaging works.

The writer is built in the separate AuroraTranslator repository. For local
development, `tools/publish-translator.ps1` publishes the sibling checkout at
`../5eApiTranslator/5eApiTranslator/AuroraTranslator.csproj` into the Git-ignored
`Aurora.App/BundledTools/AuroraTranslator` directory. Run that script only when
you intend to replace your local app's bundled writer. Its current output is
Windows x64 and framework-dependent, requiring the .NET 10 runtime. Alternatively,
supply a compatible published Translator directory from an artifact; keep its
DLLs, runtime configuration, and other dependencies beside the executable.

Run the integration explicitly from the repository root:

```powershell
.\tools\test-translator-integration.ps1 -TranslatorExecutable .\Aurora.App\BundledTools\AuroraTranslator\AuroraTranslator.exe
```

The runner validates the executable, sets `AURORA_TEST_TRANSLATOR` for the test
process, and restores its previous value afterward. An explicitly configured
missing or broken writer fails; it is not skipped. The test builds a fresh
temporary SQLite database and verifies the prepared-content contract, input
matching, secondary XML projection, and append behavior. No live database is
refreshed. Non-Windows test runs report this Windows integration as skipped.
If the test project has already been restored (for example, after running the
ordinary suite), pass `-NoRestore` to reuse those dependencies.

CI currently has no pinned Translator artifact provisioning. Ordinary tests
therefore report the integration as skipped. Release CI still needs to provision
a pinned compatible artifact, invoke this runner against the staged executable,
and verify the actual package layout on a clean machine. This test configuration
does not resolve that packaging gap.
