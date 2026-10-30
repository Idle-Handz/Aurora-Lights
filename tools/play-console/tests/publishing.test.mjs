import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { publishInternal, sha256File, PACKAGE } from '../publish-internal.mjs';
import { fingerprint, versionCode, requireVerifiedSignature } from '../build-internal.mjs';

async function fixture(run) {
  const dir = await mkdtemp(join(tmpdir(), 'aurora-publish-test-'));
  try {
    const bundlePath = join(dir, 'fixture.aab');
    await writeFile(bundlePath, 'synthetic AAB bytes; no real upload');
    const build = { packageName: PACKAGE, versionCode: 215500000, versionName: '1.0.0-internal.3',
      sourceRevision: 'a'.repeat(40), workflowRevision: 'b'.repeat(40), uploadCertificateSha256: 'c'.repeat(64), sha256: await sha256File(bundlePath) };
    await run({ bundlePath, build, token: 'test-token', reportPath: join(dir, 'report.json') });
  } finally { await rm(dir, { recursive: true, force: true }); }
}

function google(build, options = {}) {
  const calls = [];
  const fetch = async (url, request) => {
    calls.push({ url, ...request });
    assert.equal(new URL(url).hostname, 'androidpublisher.googleapis.com');
    assert.equal(request.headers.Authorization, 'Bearer test-token');
    let body = {};
    let status = 200;
    if (url.endsWith('/edits') && request.method === 'POST') body = { id: 'edit-123' };
    else if (url.endsWith('/bundles') && request.method === 'GET') body = { bundles: options.existing ?? [{ versionCode: 215469461 }] };
    else if (url.includes('uploadType=media')) {
      let bytes = 0;
      for await (const chunk of request.body) bytes += chunk.length;
      assert.equal(String(bytes), request.headers['Content-Length']);
      assert.equal(request.duplex, 'half');
      body = { versionCode: build.versionCode, sha256: options.uploadHash ?? build.sha256 };
    } else if (url.includes(':commit')) {
      assert.equal(new URL(url).searchParams.get('changesInReviewBehavior'), 'ERROR_IF_IN_REVIEW');
      if (options.commitTimeout) throw new Error('synthetic dropped connection');
      status = options.commitStatus ?? 200;
    } else if (url.endsWith('/tracks/internal/releases')) {
      if (options.readFailure) status = 503;
      body = { releases: [{ releaseName: build.versionName, track: 'internal',
        releaseLifecycleState: options.lifecycle ?? 'RELEASE_LIFECYCLE_STATE_PUBLISHED', activeArtifacts: [{ versionCode: build.versionCode }] }] };
    } else if (url.endsWith('/tracks/internal') && request.method === 'PUT') {
      const track = JSON.parse(request.body);
      assert.equal(track.track, 'internal');
      assert.deepEqual(track.releases[0].versionCodes, [String(build.versionCode)]);
      assert.equal(track.releases[0].status, 'completed');
    } else if (!url.endsWith(':validate') && request.method !== 'DELETE') throw new Error('Unexpected request: ' + url);
    return new Response(JSON.stringify(body), { status });
  };
  return { fetch, calls };
}

test('verified upload commits only internal and saves exact provenance', async () => fixture(async args => {
  const mock = google(args.build);
  const report = await publishInternal({ ...args, notes: 'Importer fixes' }, mock.fetch);
  assert.equal(report.outcome, 'published');
  assert.equal(report.commitAccepted, true);
  assert.equal(report.uploadVerified, true);
  assert.equal(report.publishedReleaseFound, true);
  assert.equal(JSON.parse(await readFile(args.reportPath)).sourceRevision, args.build.sourceRevision);
  assert.equal(mock.calls.filter(c => c.url.includes(':commit')).length, 1);
  assert.equal(mock.calls.some(c => c.method === 'DELETE'), false);
  const track = JSON.parse(mock.calls.find(c => c.method === 'PUT').body);
  assert.deepEqual(track.releases[0].releaseNotes, [{ language: 'en-US', text: 'Importer fixes' }]);
}));

test('an accepted submission still in review is not claimed published', async () => fixture(async args => {
  const mock = google(args.build, { lifecycle: 'RELEASE_LIFECYCLE_STATE_IN_REVIEW' });
  const report = await publishInternal(args, mock.fetch);
  assert.equal(report.outcome, 'submitted');
  assert.equal(report.commitAccepted, true);
  assert.equal(report.publishedReleaseFound, false);
}));

test('a changed local bundle is rejected before any network call', async () => fixture(async args => {
  await writeFile(args.bundlePath, 'changed');
  let called = false;
  await assert.rejects(publishInternal(args, async () => { called = true; }), /checksum/);
  assert.equal(called, false);
}));

test('duplicate version discards the edit without uploading', async () => fixture(async args => {
  const mock = google(args.build, { existing: [{ versionCode: args.build.versionCode }] });
  await assert.rejects(publishInternal(args, mock.fetch), /Version code/);
  assert.equal(mock.calls.some(c => c.url.includes('uploadType')), false);
  assert.equal(mock.calls.at(-1).method, 'DELETE');
}));

test('mismatched Google digest discards the edit without changing the track', async () => fixture(async args => {
  const mock = google(args.build, { uploadHash: 'e'.repeat(64) });
  await assert.rejects(publishInternal(args, mock.fetch), /upload identity/);
  assert.equal(mock.calls.some(c => c.method === 'PUT' || c.url.includes(':commit')), false);
  assert.equal(mock.calls.at(-1).method, 'DELETE');
}));

test('review rejection is never retried with cancellation options', async () => fixture(async args => {
  const mock = google(args.build, { commitStatus: 400 });
  await assert.rejects(publishInternal(args, mock.fetch), /HTTP 400/);
  assert.equal(mock.calls.filter(c => c.url.includes(':commit')).length, 1);
  assert.equal(mock.calls.at(-1).method, 'DELETE');
  assert.equal(JSON.parse(await readFile(args.reportPath)).outcome, 'failed-before-commit');
}));

test('ambiguous commit is never retried or deleted, even when status read fails', async () => fixture(async args => {
  const mock = google(args.build, { commitTimeout: true, readFailure: true });
  await assert.rejects(publishInternal(args, mock.fetch), /did not complete/);
  assert.equal(mock.calls.filter(c => c.url.includes(':commit')).length, 1);
  assert.equal(mock.calls.some(c => c.method === 'DELETE'), false);
  assert.equal(JSON.parse(await readFile(args.reportPath)).outcome, 'commit-unconfirmed');
}));

test('ambiguous commit may be confirmed published by an independent read', async () => fixture(async args => {
  const mock = google(args.build, { commitTimeout: true });
  await assert.rejects(publishInternal(args, mock.fetch), /did not complete/);
  const report = JSON.parse(await readFile(args.reportPath));
  assert.equal(report.outcome, 'published-after-unconfirmed-commit');
  assert.equal(report.publishedReleaseFound, true);
  assert.equal(mock.calls.some(c => c.method === 'DELETE'), false);
}));

test('accepted commit survives an unavailable status endpoint', async () => fixture(async args => {
  const mock = google(args.build, { readFailure: true });
  const report = await publishInternal(args, mock.fetch);
  assert.equal(report.commitAccepted, true);
  assert.equal(report.outcome, 'submitted');
  assert.ok(report.statusReadWarning);
}));

test('requires short-lived credentials and validates metadata before requests', async () => fixture(async args => {
  const noRequests = async () => { throw new Error('must not request'); };
  await assert.rejects(publishInternal({ ...args, token: '' }, noRequests), /short-lived/);
  await assert.rejects(publishInternal({ ...args, build: { ...args.build, packageName: 'other.app' } }, noRequests), /package/);
  await assert.rejects(publishInternal({ ...args, notes: 'x'.repeat(501) }, noRequests), /500/);
  await assert.rejects(publishInternal({ ...args, build: { ...args.build, versionName: '1.0;Injected=true' } }, noRequests), /version name/);
}));

test('normalizes certificates and uses the shared monotonic version sequence', () => {
  assert.equal(fingerprint('3A:F0:' + '62'.repeat(30)), '3af0' + '62'.repeat(30));
  assert.throws(() => fingerprint('SHA256: invalid'), /fingerprint/);
  assert.equal(versionCode(Date.parse('2020-01-01T00:01:00Z')), 60);
  assert.throws(() => versionCode(Date.parse('2100-01-01T00:00:00Z')), /range/);
});

test('requires positive signature verification even when Java exits zero', () => {
  assert.throws(() => requireVerifiedSignature('no manifest.\njar is unsigned.\n'), /signature/);
  assert.doesNotThrow(() => requireVerifiedSignature('\njar verified.\n\nWarning: no timestamp\n'));
});
