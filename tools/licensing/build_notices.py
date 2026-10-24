"""Render/check the offline notice bundle. No network or package execution is used."""
from __future__ import annotations
import argparse,hashlib,html,json,re,sys,xml.etree.ElementTree as ET
from pathlib import Path
from asset_inventory import verify_assets
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'third-party'
DEST=ROOT/'Aurora.App/wwwroot/legal'
def decoded(data):
 if data.startswith((b'\xff\xfe',b'\xfe\xff')):return data.decode('utf-16')
 try:return data.decode('utf-8-sig')
 except UnicodeDecodeError:return data.decode('cp1252')
def page(title,body):
 return ('<!doctype html><html lang="en"><head><meta charset="utf-8">'
 '<meta name="viewport" content="width=device-width,initial-scale=1">'
 '<meta http-equiv="Content-Security-Policy" content="default-src \'none\'; style-src \'unsafe-inline\'">'
 '<title>'+html.escape(title)+'</title><style>'
 'html{color-scheme:light dark;font:16px/1.5 system-ui,sans-serif}body{margin:1rem;max-width:65rem;overflow-wrap:anywhere}'
 'a{color:#0756a0}@media(prefers-color-scheme:dark){a{color:#8acbff}}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:14px/1.5 ui-monospace,monospace}'
 'li{margin-bottom:.7rem}h1{font-size:1.5rem}small{display:block}a:focus{outline:3px solid currentColor}'
 '</style></head><body>'+body+'</body></html>\n')
def expected(data):
 assets,_=verify_assets(data)
 source_manifest=json.loads((SOURCE/'sources/manifest.json').read_text(encoding='utf-8'))
 archive=SOURCE/'sources'/source_manifest['archive']
 if not archive.resolve().is_relative_to(SOURCE.resolve()):raise ValueError('Source archive path escapes third-party directory')
 if hashlib.sha256(archive.read_bytes()).hexdigest()!=source_manifest['sha256']:raise ValueError('LGPL corresponding source archive hash mismatch')
 for project in ['Aurora.Documents/Aurora.Documents.csproj','Aurora.Logic/Aurora.Logic.csproj','Aurora.Lights/Aurora.Legacy.csproj','Aurora.Web/Aurora.Web.csproj']:
  xml=ET.fromstring((ROOT/project).read_bytes())
  if any(e.get('Include','').lower()=='itextsharp' for e in xml.iter('Reference')):raise ValueError('Legacy AGPL PDF reference in '+project)
  refs=[e for e in xml.iter('PackageReference') if e.get('Include')=='iTextSharp.LGPLv2.Core']
  if len(refs)!=1 or refs[0].get('Version')!=source_manifest['version']:raise ValueError('PDF dependency does not match corresponding source: '+project)
 result={};texts={}
 own=hashlib.sha256((ROOT/'LICENSE').read_text(encoding='utf-8').encode('utf-8')).hexdigest()
 if own!=data['ownLicenseText']:raise ValueError('The original-code license changed; update its notice record')
 for component in data['components']:
  for field,digest in [('binaryPath','binarySha256'),('noticePath','noticeSha256')]:
   if field in component:
    source=(ROOT/component[field]).resolve()
    if not source.is_relative_to(ROOT):raise ValueError('Source path escapes repository')
    if hashlib.sha256(source.read_bytes()).hexdigest()!=component[digest]:raise ValueError('Notice source changed: '+component[field])

 ids={data['ownLicenseText']}|{t['sha256'] for c in data['components'] for t in c['texts']}
 for digest in sorted(ids):
  if not re.fullmatch('[0-9a-f]{64}',digest):raise ValueError('Invalid legal-text identifier')
  raw=(SOURCE/'texts'/f'{digest}.txt').read_bytes()
  if hashlib.sha256(raw).hexdigest()!=digest:raise ValueError('Legal text hash mismatch: '+digest)
  text=decoded(raw);texts[digest]=text
  result[f'{digest}.html']=page('License text','<p><a href="index.html">Back to licenses and notices</a></p><h1>License text</h1><pre>'+html.escape(text)+'</pre>').encode('utf-8')
 rows=[];plain=['Aurora Lights — licenses and third-party notices','',texts[data['ownLicenseText']],'']
 for c in data['components']:
  label=c['name']+' '+c['version'];links=[]
  plain.extend([label,'License: '+c['license']])
  if c.get('copyright'):plain.append(c['copyright'])
  if c.get('authors'):plain.append('Authors: '+c['authors'])
  # License expressions and notices remain visible alongside the complete verbatim texts.
  if c.get('copyright'):links.append('<small>'+html.escape(c['copyright'])+'</small>')
  for i,t in enumerate(c['texts'],1):
   links.append(f'<a href="{t["sha256"]}.html">License or notice {i}</a>')
   plain.extend(['Source: '+t['origin'],'Text SHA-256: '+t['sha256']])
  rows.append('<li><b>'+html.escape(label)+'</b><small>'+html.escape(c['license'])+'</small>'+' '.join(links)+'</li>')
  plain.append('')
 asset_groups={group['id']:group for group in assets['groups']}
 asset_rows=[]
 plain.extend(['Bundled content and asset provenance','The inventory records scope and evidence; pending records do not grant redistribution rights.',''])
 for group in assets['groups']:
  plain.extend([group['name'],'License: '+group['license'],'Review state: '+group['state'],*group.get('evidence',[]),group.get('reason',''),''])
 for entry in assets['files']:
  group=asset_groups[entry['group']]
  asset_rows.append('<li><b>'+html.escape(entry['path'])+'</b><small>'+html.escape(group['name']+'; '+group['license']+'; '+group['state'])+'</small><small>SHA-256: '+entry['sha256']+'</small></li>')
  plain.extend([entry['path'],'SHA-256: '+entry['sha256'],'Asset group: '+entry['group']])
 result['assets.html']=page('Bundled content and assets','<p><a href="index.html">Back to licenses and notices</a></p><h1>Bundled content and assets</h1><p>File hashes identify reviewed versions. Pending records do not grant redistribution rights.</p><ul>'+''.join(asset_rows)+'</ul>').encode('utf-8')
 plain.extend(['','Full license and notice texts',''])
 for digest,text in sorted(texts.items()):plain.extend(['='*72,'Text SHA-256: '+digest,'',text,''])
 result['index.html']=page('Licenses and notices','<h1>Licenses and notices</h1><p>Aurora Lights original code is licensed under MIT. The components below retain their own licenses and notices.</p>'
  +'<p>PDF processing uses iTextSharp.LGPLv2.Core under LGPL v2. Library copyright: Vahid Nasiri and original iText contributors, including Bruno Lowagie and Paulo Soares. Modification for your own use and reverse engineering to debug modifications are permitted. Matching library and application source plus rebuild instructions accompany each release as aurora-corresponding-source.zip.</p>'
  +f'<p><a href="{data["ownLicenseText"]}.html">Aurora Lights MIT license</a> · <a href="assets.html">Bundled content and asset provenance</a></p><ul>'+''.join(rows)+'</ul>').encode('utf-8')
 result['THIRD-PARTY-NOTICES.txt']=('\n'.join(plain)+'\n').encode('utf-8')
 # Generated pages follow the repository's LF checkout policy. Preserve the
 # hashed upstream text bytes in texts/, but do not embed platform-dependent
 # newlines that would make a fresh checkout fail the generated-file check.
 return {name:content.replace(b'\r\n',b'\n').replace(b'\r',b'\n') for name,content in result.items()}

def verify_dependencies(path,platform,data):
 doc=json.loads(path.read_text(encoding='utf-8-sig'))
 if any(n.lower().startswith('itextsharp/') for n in doc.get('libraries',{})):raise ValueError('Legacy AGPL PDF assembly remains in published dependencies')
 if 'targets' in doc:
  targets={k:v for k,v in doc['targets'].items() if (platform=='android' and 'android' in k.lower()) or (platform=='windows' and ('windows' in k.lower() or '/win-' in k.lower()))}
  if not targets:raise ValueError('No '+platform+' dependency target in '+str(path))
  actual={k for t in targets.values() for k,v in t.items() if v.get('type',doc.get('libraries',{}).get(k,{}).get('type'))=='package'}
 else:raise ValueError('Expected project.assets.json or a deps.json with target records')
 known={c['name'].lower()+'/'+c['version'].lower() for c in data['components'] if platform in c['platforms']}
 missing=sorted(x for x in actual if x.lower() not in known)
 if missing:raise ValueError('Dependencies missing from notices: '+', '.join(missing))
 print(f'{platform}: {len(actual)} package dependencies have notice records.')

def main():
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('--check',action='store_true')
 parser.add_argument('--platform',choices=['windows','android'])
 parser.add_argument('--dependencies',type=Path)
 parser.add_argument('--require-resolved',action='store_true')
 args=parser.parse_args()
 data=json.loads((SOURCE/'components.json').read_text(encoding='utf-8'))
 files=expected(data)
 if args.check:
  stale=[name for name,content in files.items() if not (DEST/name).is_file() or (DEST/name).read_bytes()!=content]
  extras=sorted(p.name for p in DEST.iterdir() if p.is_file() and p.name not in files) if DEST.exists() else []
  if stale or extras:raise ValueError('Regenerate notices. Stale/missing: '+', '.join(stale)+'; unexpected: '+', '.join(extras))
 else:
  DEST.mkdir(parents=True,exist_ok=True)
  for name,content in files.items():(DEST/name).write_bytes(content)
  extras=[p.name for p in DEST.iterdir() if p.is_file() and p.name not in files]
  if extras:raise ValueError('Unexpected generated files require review: '+', '.join(extras))
 if bool(args.dependencies)!=bool(args.platform):raise ValueError('--dependencies and --platform must be supplied together')
 if args.dependencies:verify_dependencies(args.dependencies,args.platform,data)
 if args.require_resolved:
  pending=[c['name'] for c in data['components'] if c.get('state')!='recorded' and (not args.platform or args.platform in c['platforms'])]
  _,asset_pending=verify_assets(data,distribution=args.platform)
  pending+=asset_pending
  pending+= [x['id'] for x in data.get('releaseIssues',[]) if x['status']!='closed']
  if pending:raise ValueError('Release licensing is unresolved: '+', '.join(pending))
 print(f'{len(data["components"])} components; {len(files)} generated files; legal text hashes and generated bundle verified.')
if __name__=='__main__':
 try:main()
 except (ValueError,OSError,UnicodeError) as ex:print(str(ex),file=sys.stderr);sys.exit(1)
