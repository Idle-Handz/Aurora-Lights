"""Create matching release source materials without private configuration or the old AGPL PDF engine."""
from pathlib import Path
import argparse, hashlib, json, subprocess, zipfile
from asset_inventory import verify_assets
ROOT = Path(__file__).resolve().parents[2]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--require-resolved', action='store_true', help='Reject source distribution with pending asset permissions')
    args = parser.parse_args()
    components = json.loads((ROOT/'third-party/components.json').read_text(encoding='utf-8'))
    _, pending_assets = verify_assets(components, distribution='source')
    if args.require_resolved:
        pending = pending_assets + [c['name'] for c in components['components'] if c.get('state') != 'recorded']
        pending += [issue['id'] for issue in components.get('releaseIssues', []) if issue['status'] != 'closed']
        if pending:
            raise ValueError('Source distribution licensing is unresolved: ' + ', '.join(pending))
        from build_notices import expected
        expected(components)  # Also verify all notice/source hashes before creating an archive.
    manifest = json.loads((ROOT/'third-party/sources/manifest.json').read_text(encoding='utf-8'))
    source = ROOT/'third-party/sources'/manifest['archive']
    if hashlib.sha256(source.read_bytes()).hexdigest() != manifest['sha256']:
        raise ValueError('LGPL source archive hash mismatch')
    # Include source under review as well as committed source; exclude all scratch,
    # generated outputs, credentials, and historical third-party oracle dependencies.
    names = subprocess.check_output(['git','ls-files','-z','--cached','--others','--exclude-standard'], cwd=ROOT).decode().split('\0')
    tracked = set(subprocess.check_output(['git','ls-files','-z','--cached'],cwd=ROOT).decode().split('\0'))
    allowed = {'tests','.github','Aurora.Reflections','Aurora.Components','Aurora.DataIntegration','Aurora.Documents','Aurora.Legacy','Aurora.Logic','Aurora.PdfImport','Aurora.Web','Aurora.Tests','Aurora.Presentation','Aurora.Presentation.Tests','Builder.Core','Builder.Data','vendor','third-party','tools','docs','lib'}
    root_files = {'package.json','package-lock.json','LICENSE','README.md','global.json','NuGet.Config','nuget.config','AuroraContent.props','.gitattributes','.gitignore','Directory.Build.props','Directory.Build.targets'}
    files = []
    for name in sorted(set(filter(None,names))):
        p = Path(name)
        if p.parts[0] not in allowed and name not in root_files and not (len(p.parts)==1 and p.suffix in {'.sln','.slnx'}): continue
        if any(part in {'bin','obj','node_modules','__pycache__','.git','private','Configuration'} for part in p.parts): continue
        if p.parts[0]=='docs' and name not in tracked and name not in {'docs/pdf-library-and-notices-2026-10-23.md','docs/android-pdf-export-acceptance-2026-10-23.md','docs/bundled-content-and-assets-2026-10-23.md','docs/v1-public-baseline-migration.md'}: continue
        # Production publication/signing configuration is not required for relinking.
        if name=='.github/workflows/release.yml': continue
        # Historical oracle binaries are unnecessary for building or relinking
        # the native application; retain their source-native replacements instead.
        if 'LegacyOracles' in p.parts: continue
        # Saved-character fixtures contain historical imported descriptions;
        # keep them intact locally, outside the distributable application source.
        if name.startswith('Aurora.Tests/Fixtures/Characters/'): continue
        if p.suffix.lower() in {'.jks','.pfx','.p12','.keystore','.user','.suo','.log'}: continue
        if name.startswith('lib/') and p.name != 'DynamicExpresso.Core.dll': continue
        if name in {'third-party/legacy-test-only.json'}: continue
        if not (ROOT/p).is_file(): continue
        files.append(name)
    for required in ['Aurora.Reflections/Aurora.Reflections.csproj','Aurora.Documents/Aurora.Documents.csproj','Aurora.Logic/Models/CharacterSheet/CharacterSheetEx.cs','third-party/LGPL-RELINKING.md','third-party/sources/'+manifest['archive']]:
        if required not in files: raise ValueError('Required corresponding source missing: '+required)
    hashes = {name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in files}
    record = {'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(), 'files':hashes, 'note':'Hashes identify the actual source, including any uncommitted changes; revision alone is insufficient.'}
    args.output.parent.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(args.output,'w',zipfile.ZIP_DEFLATED) as z:
        for name in files: z.write(ROOT/name,name)
        z.writestr('SOURCE-MANIFEST.json',json.dumps(record,indent=2)+'\n')
    print(f'Created matching source bundle: {len(files)} files; SHA-256 '+hashlib.sha256(args.output.read_bytes()).hexdigest())
if __name__ == '__main__': main()
