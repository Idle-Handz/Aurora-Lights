import { createReadStream } from 'node:fs';
import { readFile, writeFile, stat } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { PlayClient, validateVersion } from './play-client.mjs';

const API = 'https://androidpublisher.googleapis.com/androidpublisher/v3/applications/com.auroralights.app';
const UPLOAD = 'https://androidpublisher.googleapis.com/upload/androidpublisher/v3/applications/com.auroralights.app';
export const PACKAGE = 'com.auroralights.app';

export async function sha256File(path) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(path)) hash.update(chunk);
  return hash.digest('hex');
}

export function validateBuild(build) {
  if (build?.packageName !== PACKAGE) throw new Error('Bundle package must be ' + PACKAGE + '.');
  validateVersion(build.versionCode);
  if (typeof build.versionName !== 'string' || !/^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$/.test(build.versionName))
    throw new Error('Use a version name such as 1.0.0-internal.3.');
  for (const field of ['sha256', 'uploadCertificateSha256'])
    if (!/^[a-f0-9]{64}$/.test(build[field])) throw new Error('Invalid ' + field + '.');
  for (const field of ['sourceRevision', 'workflowRevision'])
    if (!/^[a-f0-9]{40}$/.test(build[field])) throw new Error('Invalid ' + field + '.');
  return build;
}

// Deliberately separate from the read-only MCP. No configurable package or track.
export async function publishInternal({ bundlePath, build, token, notes = '', reportPath }, fetchImpl = fetch) {
  validateBuild(build);
  if (!token) throw new Error('A short-lived GOOGLE_OAUTH_ACCESS_TOKEN is required.');
  if ([...notes].length > 500) throw new Error('Release notes must be at most 500 characters.');
  if (await sha256File(bundlePath) !== build.sha256) throw new Error('Bundle checksum differs from the verified build.');
  const size = (await stat(bundlePath)).size;
  const report = { ...build, track: 'internal', commitAccepted: false, publishedReleaseFound: false, outcome: 'not-submitted' };
  const save = async () => { if (reportPath) await writeFile(reportPath, JSON.stringify(report, null, 2) + '\n'); };
  let editId;
  let commitStarted = false;
  let commitRejected = false;
  async function request(url, method, body, upload = false) {
    let response;
    try {
      response = await fetchImpl(url, {
        method, redirect: 'error', signal: AbortSignal.timeout(upload ? 600000 : 30000),
        headers: { Authorization: 'Bearer ' + token, ...(body ? { 'Content-Type': upload ? 'application/octet-stream' : 'application/json' } : {}),
          ...(upload ? { 'Content-Length': String(size) } : {}) },
        ...(body ? { body: upload ? body : JSON.stringify(body) } : {}), ...(upload ? { duplex: 'half' } : {}),
      });
    } catch { throw new Error('Google request did not complete. Check the release report before retrying.'); }
    if (!response.ok) {
      if (method === 'POST' && url.includes(':commit')) commitRejected = response.status >= 400 && response.status < 500;
      const hint = response.status === 403 ? ' Check publisher Play permissions and WIF access.' :
        response.status === 400 ? ' Check Play Console for review, signing, version, or app-setup requirements.' : '';
      throw new Error('Google Play returned HTTP ' + response.status + '.' + hint);
    }
    if (method === 'DELETE') return null;
    try { return await response.json(); }
    catch { throw new Error('Google returned an invalid response.'); }
  }
  async function inspect() {
    const reader = new PlayClient({ packageName: PACKAGE, defaultTrack: 'internal', accessToken: token }, fetchImpl);
    const state = await reader.findVersion(build.versionCode);
    report.publishedReleaseFound = state.publishedReleaseFound;
    report.releases = state.releases;
    report.checkedAtUtc = state.checkedAtUtc;
  }
  await save();
  try {
    const edit = await request(API + '/edits', 'POST', {});
    if (!/^[A-Za-z0-9_-]+$/.test(edit?.id ?? '')) throw new Error('Google did not return a valid edit ID.');
    editId = edit.id;
    report.editId = editId;
    const editUrl = API + '/edits/' + editId;
    const existing = await request(editUrl + '/bundles', 'GET');
    if (!Array.isArray(existing.bundles ?? [])) throw new Error('Invalid existing bundle list.');
    const versions = (existing.bundles ?? []).map(b => validateVersion(Number(b.versionCode)));
    if (versions.some(v => v >= build.versionCode)) throw new Error('Version code must exceed every previously uploaded bundle. Start a new workflow run.');
    const uploaded = await request(UPLOAD + '/edits/' + editId + '/bundles?uploadType=media', 'POST', createReadStream(bundlePath), true);
    if (Number(uploaded.versionCode) !== build.versionCode || uploaded.sha256?.toLowerCase() !== build.sha256)
      throw new Error('Google upload identity differs from the verified local bundle.');
    report.uploadVerified = true;
    const release = { name: build.versionName, versionCodes: [String(build.versionCode)], status: 'completed' };
    if (notes.trim()) release.releaseNotes = [{ language: 'en-US', text: notes.trim() }];
    await request(editUrl + '/tracks/internal', 'PUT', { track: 'internal', releases: [release] });
    await request(editUrl + ':validate', 'POST');
    report.outcome = 'commit-started';
    await save();
    commitStarted = true;
    // Google's default cancels an existing review; explicitly refuse that behavior.
    await request(editUrl + ':commit?changesInReviewBehavior=ERROR_IF_IN_REVIEW', 'POST');
    report.commitAccepted = true;
    report.outcome = 'submitted';
    try { await inspect(); } catch { report.statusReadWarning = 'Submission accepted; release availability could not yet be read.'; }
    if (report.publishedReleaseFound) report.outcome = 'published';
    await save();
    return report;
  } catch (error) {
    report.error = error.message;
    if (commitStarted && !commitRejected && !report.commitAccepted) {
      report.outcome = 'commit-unconfirmed';
      try { await inspect(); } catch { /* Preserve ambiguity; do not retry the write. */ }
      if (report.publishedReleaseFound) report.outcome = 'published-after-unconfirmed-commit';
    } else if (!report.commitAccepted) {
      report.outcome = 'failed-before-commit';
      if (editId) {
        try { await request(API + '/edits/' + editId, 'DELETE'); report.editDiscarded = true; }
        catch { report.cleanupWarning = 'The uncommitted edit could not be discarded; it may need Console attention or expiry.'; }
      }
    }
    await save();
    throw error;
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const build = JSON.parse(await readFile(process.env.PLAY_BUILD_MANIFEST, 'utf8'));
    const report = await publishInternal({ bundlePath: process.env.AAB_PATH, build,
      token: process.env.GOOGLE_OAUTH_ACCESS_TOKEN, notes: process.env.RELEASE_NOTES || '', reportPath: process.env.PLAY_REPORT_PATH });
    console.log('Internal release: ' + report.outcome + ' (version ' + report.versionCode + ').');
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
