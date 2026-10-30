import { readFile, writeFile, mkdir, mkdtemp, readdir, copyFile, rm } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { PACKAGE, sha256File, validateBuild } from './publish-internal.mjs';

export function fingerprint(value) {
  const normalized = (value || '').replace(/[:\s]/g, '').toLowerCase();
  if (!/^[a-f0-9]{64}$/.test(normalized)) throw new Error('Set GOOGLE_PLAY_UPLOAD_CERT_SHA256 to the Play upload certificate fingerprint.');
  return normalized;
}

export function versionCode(now = Date.now()) {
  const code = Math.floor(now / 1000) - 1577836800;
  if (code < 1 || code > 2100000000) throw new Error('Generated version code is outside the Android range.');
  return code;
}

export function requireVerifiedSignature(output) {
  // jarsigner -verify -strict can exit zero for a completely unsigned archive.
  if (!/^\s*jar verified\.\s*$/m.test(output || '')) throw new Error('AAB does not have a verified upload-key signature.');
}

export async function buildInternal(env = process.env) {
  const source = resolve(env.RELEASE_SOURCE);
  const output = resolve(env.RELEASE_OUTPUT);
  const project = 'Aurora.Reflections/Aurora.Reflections.csproj';
  await readFile(join(source, project)).catch(() => { throw new Error('Selected source must contain Aurora.Reflections. Use the release-preparation branch until it is merged into main.'); });
  const code = versionCode();
  const expected = fingerprint(env.GOOGLE_PLAY_UPLOAD_CERT_SHA256);
  const build = { packageName: PACKAGE, versionCode: code, versionName: env.RELEASE_VERSION,
    sourceRevision: '', workflowRevision: env.GITHUB_SHA, sha256: '0'.repeat(64), uploadCertificateSha256: expected };
  const run = (command, args, capture = false) => {
    try { return execFileSync(command, args, { cwd: source, encoding: 'utf8', stdio: capture ? ['ignore', 'pipe', 'inherit'] : 'inherit', maxBuffer: 16 * 1024 * 1024 })?.trim(); }
    catch { throw new Error(command + ' failed. See the build step for details.'); }
  };
  build.sourceRevision = run('git', ['rev-parse', 'HEAD'], true);
  validateBuild(build);
  for (const key of ['ANDROID_UPLOAD_KEYSTORE_BASE64', 'ANDROID_UPLOAD_KEY_ALIAS', 'ANDROID_UPLOAD_KEY_PASSWORD', 'ANDROID_UPLOAD_STORE_PASSWORD'])
    if (!env[key]) throw new Error('Missing environment secret ' + key + '.');
  if (!/^[A-Za-z0-9_.-]{1,100}$/.test(env.ANDROID_UPLOAD_KEY_ALIAS)) throw new Error('Unsupported upload-key alias.');
  if (/[\r\n]/.test(env.ANDROID_UPLOAD_KEY_PASSWORD + env.ANDROID_UPLOAD_STORE_PASSWORD)) throw new Error('Signing passwords must be single-line values.');
  const encoded = env.ANDROID_UPLOAD_KEYSTORE_BASE64.replace(/\s/g, '');
  if (!/^[A-Za-z0-9+/]+={0,2}$/.test(encoded) || encoded.length % 4 !== 0) throw new Error('Upload keystore must be valid Base64.');
  await mkdir(output, { recursive: true });
  const temp = await mkdtemp(join(env.RUNNER_TEMP, 'aurora-signing-'));
  const store = join(temp, 'upload.jks'), storePass = join(temp, 'store-pass'), keyPass = join(temp, 'key-pass');
  try {
    await writeFile(store, Buffer.from(encoded, 'base64'), { mode: 0o600 });
    await writeFile(storePass, env.ANDROID_UPLOAD_STORE_PASSWORD, { mode: 0o600 });
    await writeFile(keyPass, env.ANDROID_UPLOAD_KEY_PASSWORD, { mode: 0o600 });
    const cert = join(temp, 'upload.der');
    run('keytool', ['-exportcert', '-keystore', store, '-storepass:file', storePass, '-alias', env.ANDROID_UPLOAD_KEY_ALIAS, '-file', cert]);
    if (createHash('sha256').update(await readFile(cert)).digest('hex') !== expected)
      throw new Error('Upload certificate does not match the approved Play upload identity.');
    const config = join(temp, 'bundle-config.json');
    await writeFile(config, JSON.stringify({ optimizations: { uncompressNativeLibraries: { enabled: true, alignment: 'PAGE_ALIGNMENT_16K' } } }));
    run('dotnet', ['restore', project, '-r', 'android-arm64', '-p:TargetFramework=net10.0-android']);
    run('python3', ['tools/licensing/build_notices.py', '--check', '--platform', 'android', '--dependencies', 'Aurora.Reflections/obj/project.assets.json', '--require-resolved']);
    // Capture matching source before generating build outputs. Signing files live outside this checkout.
    run('python3', ['tools/licensing/build_source_bundle.py', '--output', join(output, 'aurora-corresponding-source.zip'), '--require-resolved']);
    const assembly = build.versionName.split('-')[0];
    run('dotnet', ['publish', project, '-f', 'net10.0-android', '-r', 'android-arm64', '-c', 'Release', '--no-restore',
      '-p:AndroidPackageFormats=aab', '-p:AndroidKeyStore=true', '-p:AndroidSigningKeyStore=' + store,
      '-p:AndroidSigningKeyAlias=' + env.ANDROID_UPLOAD_KEY_ALIAS, '-p:AndroidSigningKeyPass=file:' + keyPass,
      '-p:AndroidSigningStorePass=file:' + storePass, '-p:AndroidBundleConfigurationFile=' + config,
      '-p:RunAOTCompilation=false', '-p:ApplicationVersion=' + code, '-p:ApplicationDisplayVersion=' + build.versionName,
      '-p:Version=' + build.versionName, '-p:AssemblyVersion=' + assembly + '.0', '-p:FileVersion=' + assembly + '.0',
      '-p:InformationalVersion=' + build.versionName + '+' + build.sourceRevision]);
    const publish = join(source, 'Aurora.Reflections/bin/Release/net10.0-android/android-arm64/publish');
    const bundles = (await readdir(publish)).filter(name => name.endsWith('-Signed.aab'));
    if (bundles.length !== 1) throw new Error('Expected exactly one signed AAB; unsigned fallback is prohibited.');
    const bundle = join(publish, bundles[0]);
    requireVerifiedSignature(run('jarsigner', ['-J-Duser.language=en', '-J-Duser.country=US', '-verify', '-strict', '-keystore', store, '-storepass:file', storePass, bundle, env.ANDROID_UPLOAD_KEY_ALIAS], true));
    const tool = ['-jar', resolve(env.BUNDLETOOL_PATH)];
    run('java', [...tool, 'validate', '--bundle=' + bundle]);
    const dump = xpath => run('java', [...tool, 'dump', 'manifest', '--bundle=' + bundle, '--xpath=' + xpath], true);
    if (dump('/manifest/@package') !== PACKAGE || dump('/manifest/@android:versionCode') !== String(code) || dump('/manifest/@android:versionName') !== build.versionName)
      throw new Error('Packaged app identity or version differs from requested release.');
    if (Number(dump('/manifest/uses-sdk/@android:targetSdkVersion')) < 36) throw new Error('Bundle must target Android API 36 or newer.');
    const bundleConfig = JSON.parse(run('java', [...tool, 'dump', 'config', '--bundle=' + bundle], true));
    if (bundleConfig.optimizations?.uncompressNativeLibraries?.alignment !== 'PAGE_ALIGNMENT_16K') throw new Error('Bundle does not request 16 KB native-library alignment.');
    const destination = join(output, 'aurora-internal.aab');
    await copyFile(bundle, destination);
    build.sha256 = await sha256File(destination);
    build.correspondingSourceSha256 = await sha256File(join(output, 'aurora-corresponding-source.zip'));
    build.builtAtUtc = new Date().toISOString();
    await writeFile(join(output, 'build-manifest.json'), JSON.stringify(build, null, 2) + '\n');
    console.log('Verified signed internal AAB: ' + code + ', source ' + build.sourceRevision + '.');
    return build;
  } finally { await rm(temp, { recursive: true, force: true }); }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { await buildInternal(); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
