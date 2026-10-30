import test from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync, verify } from 'node:crypto';
import { mkdir, mkdtemp, readFile, realpath, rm, writeFile } from 'node:fs/promises';
import { join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { PlayClient, PlayError, PUBLISHED, settingsFromEnvironment } from '../play-client.mjs';
import { createTools, TOOLS, runStdio } from '../server.mjs';
import { PassThrough } from 'node:stream';

const release = (code, state = PUBLISHED) => ({
  releaseName: 'Build ' + code, track: 'internal',
  activeArtifacts: [{ versionCode: code }], releaseLifecycleState: state,
});
const makeClient = (payload, status = 200) => {
  const calls = [];
  const settings = settingsFromEnvironment({ GOOGLE_OAUTH_ACCESS_TOKEN: 'synthetic-token' });
  const client = new PlayClient(settings, async (url, options) => {
    calls.push({ url, options });
    return new Response(JSON.stringify(payload), { status });
  });
  return { client, calls };
};

test('reads the direct endpoint and preserves review state separately', async () => {
  const { client, calls } = makeClient({ releases: [
    release(215469461), release(215469462, 'RELEASE_LIFECYCLE_STATE_IN_REVIEW'),
  ] });
  const result = await client.getReleases();
  assert.equal(result.highestPublishedVersionCode, 215469461);
  assert.equal(result.releases.length, 2);
  assert.equal(calls[0].url, 'https://androidpublisher.googleapis.com/androidpublisher/v3/applications/com.auroralights.app/tracks/internal/releases');
  assert.equal(calls[0].options.method, 'GET');
  assert.equal(calls[0].options.redirect, 'error');
  assert.match(result.limitations, /halted/);
});

test('matches exact codes without treating pending review as publication', async () => {
  const { client } = makeClient({ releases: [
    release(215469461), release(215469462, 'RELEASE_LIFECYCLE_STATE_IN_REVIEW'),
  ] });
  assert.equal((await client.findVersion(215469461)).publishedReleaseFound, true);
  const pending = await client.findVersion(215469462);
  assert.equal(pending.found, true);
  assert.equal(pending.publishedReleaseFound, false);
  assert.equal((await client.findVersion(100)).found, false);
});

test('accepts empty tracks without inventing a current release', async () => {
  const result = await makeClient({}).client.getReleases();
  assert.deepEqual(result.releases, []);
  assert.equal(result.highestPublishedVersionCode, null);
});

test('validates package, track and version arguments before network access', async () => {
  assert.throws(() => settingsFromEnvironment({ AURORA_PLAY_PACKAGE: '../other' }), PlayError);
  const { client, calls } = makeClient({});
  for (const code of [true, 0, 2100000001, '215469461'])
    await assert.rejects(client.findVersion(code), PlayError);
  for (const track of ['', '../production', 'https://example.com'])
    await assert.rejects(client.getReleases(track), PlayError);
  assert.equal(calls.length, 0);
});

test('rejects malformed or mismatched Google data', async () => {
  for (const data of [[], { releases: null }, { releases: [null] },
    { releases: [release(true)] }, { releases: [{ ...release(1), track: 'production' }] }]) {
    await assert.rejects(makeClient(data).client.getReleases(), PlayError);
  }
});

test('does not expose HTTP response bodies in errors', async () => {
  await assert.rejects(makeClient({ error: { message: 'secret-response-value' } }, 403).client.getReleases(),
    error => /View app information/.test(error.message) && !error.message.includes('secret-response-value'));
});

test('network failures are bounded and sanitized', async () => {
  const client = new PlayClient(settingsFromEnvironment({ GOOGLE_OAUTH_ACCESS_TOKEN: 'synthetic-token' }),
    async () => { throw new Error('sensitive transport diagnostic'); });
  await assert.rejects(client.getReleases(), error =>
    error instanceof PlayError && !error.message.includes('sensitive transport diagnostic'));
});

test('cancelled reads do not start a request', async () => {
  const { client, calls } = makeClient({});
  const controller = new AbortController();
  controller.abort();
  await assert.rejects(client.getReleases(undefined, controller.signal), /cancelled/);
  assert.equal(calls.length, 0);
});

test('service account JWT has the correct signature, scope, audience and token caching', async () => {
  const scratchRoot = fileURLToPath(new URL('../.test-cache/', import.meta.url));
  await mkdir(scratchRoot, { recursive: true });
  const directory = await mkdtemp(join(scratchRoot, 'auth-'));
  try {
    const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
    const filename = join(directory, 'synthetic-key.json');
    await writeFile(filename, JSON.stringify({
      type: 'service_account', client_email: 'reader@example.iam.gserviceaccount.com',
      private_key_id: 'synthetic-key', token_uri: 'https://oauth2.googleapis.com/token',
      private_key: privateKey.export({ type: 'pkcs8', format: 'pem' }),
    }));
    let tokenRequests = 0;
    const client = new PlayClient(settingsFromEnvironment({ GOOGLE_APPLICATION_CREDENTIALS: filename }),
      async (url, options) => {
        if (url === 'https://oauth2.googleapis.com/token') {
          tokenRequests++;
          assert.equal(options.method, 'POST');
          assert.equal(options.redirect, 'error');
          const form = new URLSearchParams(options.body);
          assert.equal(form.get('grant_type'), 'urn:ietf:params:oauth:grant-type:jwt-bearer');
          const [header, payload, signature] = form.get('assertion').split('.');
          assert.equal(JSON.parse(Buffer.from(header, 'base64url')).alg, 'RS256');
          const claims = JSON.parse(Buffer.from(payload, 'base64url'));
          assert.equal(claims.iss, 'reader@example.iam.gserviceaccount.com');
          assert.equal(claims.aud, 'https://oauth2.googleapis.com/token');
          assert.equal(claims.scope, 'https://www.googleapis.com/auth/androidpublisher');
          assert.equal(claims.exp - claims.iat, 3600);
          assert.equal(verify('RSA-SHA256', Buffer.from(header + '.' + payload), publicKey, Buffer.from(signature, 'base64url')), true);
          return new Response(JSON.stringify({ access_token: 'synthetic-result', expires_in: 3600 }));
        }
        assert.equal(options.headers.Authorization, 'Bearer synthetic-result');
        return new Response(JSON.stringify({ releases: [release(215469461)] }));
      });
    await client.getReleases();
    await client.getReleases();
    assert.equal(tokenRequests, 1);
  } finally {
    const actual = await realpath(directory);
    const expectedRoot = await realpath(scratchRoot);
    assert.ok(actual.startsWith(expectedRoot + sep));
    await rm(actual, { recursive: true });
  }
});

test('tools accept only declared arguments and have read-only metadata', async () => {
  for (const tool of TOOLS) {
    assert.equal(tool.annotations.readOnlyHint, true);
    assert.equal(tool.annotations.destructiveHint, false);
  }
  const call = createTools({});
  await assert.rejects(call('play_get_releases', { credential_file: 'untrusted' }), /Unexpected/);
  await assert.rejects(call('upload_release', {}), /Unknown/);
});

test('protocol rejects premature calls and malformed JSON without leaving stdout', async () => {
  const input = new PassThrough();
  const output = new PassThrough();
  let messages = '';
  output.on('data', data => { messages += data; });
  runStdio(input, output, async () => ({}));
  input.write('invalid\n');
  input.write(JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'tools/list' }) + '\n');
  await new Promise(resolve => setImmediate(resolve));
  const results = messages.trim().split('\n').map(line => JSON.parse(line));
  assert.equal(results[0].error.code, -32700);
  assert.equal(results[1].error.code, -32002);
  input.end();
});

test('real subprocess handshake discovers tools and diagnoses a missing key', async t => {
  const serverPath = fileURLToPath(new URL('../server.mjs', import.meta.url));
  const child = spawn(process.execPath, [serverPath], {
    env: { ...process.env, GOOGLE_APPLICATION_CREDENTIALS: resolve(serverPath, '../not-present.json'),
      GOOGLE_OAUTH_ACCESS_TOKEN: '', AURORA_PLAY_TRACK: 'internal' },
    stdio: ['pipe', 'pipe', 'pipe'],
  });
  t.after(() => { child.stdin.end(); child.kill(); });
  let stderr = '';
  child.stderr.on('data', data => { stderr += data; });
  const waiting = new Map();
  const lines = createInterface({ input: child.stdout });
  lines.on('line', line => {
    const result = JSON.parse(line);
    waiting.get(result.id)?.(result);
    waiting.delete(result.id);
  });
  let id = 0;
  const request = (method, params) => {
    const requestId = ++id;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('MCP timeout: ' + stderr)), 5000);
      waiting.set(requestId, result => { clearTimeout(timer); resolve(result); });
      child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id: requestId, method, params }) + '\n');
    });
  };
  const initialized = await request('initialize', {
    protocolVersion: '2025-11-25', capabilities: {}, clientInfo: { name: 'test', version: '1' },
  });
  assert.equal(initialized.result.protocolVersion, '2025-11-25');
  child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }) + '\n');
  const listed = await request('tools/list', {});
  assert.deepEqual(listed.result.tools.map(tool => tool.name), TOOLS.map(tool => tool.name));
  const check = await request('tools/call', { name: 'play_check_connection', arguments: {} });
  assert.equal(check.result.isError, false);
  assert.equal(check.result.structuredContent.connected, false);
  assert.match(check.result.structuredContent.error, /service account JSON/);
});
