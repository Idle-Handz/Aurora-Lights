import { readFile } from 'node:fs/promises';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { createPrivateKey, sign } from 'node:crypto';

const ROOT = 'https://androidpublisher.googleapis.com/androidpublisher/v3';
const TOKEN_URL = 'https://oauth2.googleapis.com/token';
const SCOPE = 'https://www.googleapis.com/auth/androidpublisher';
export const PUBLISHED = 'RELEASE_LIFECYCLE_STATE_PUBLISHED';
export const LIMITATIONS = 'At most 20 non-obsolete releases. Published includes partial rollouts and resumable halted releases. This does not establish universal availability, rollout percentage, publication time, release notes, source revision, or bundle contents.';

export class PlayError extends Error {}

export function settingsFromEnvironment(env = process.env) {
  const settings = {
    credentialFile: env.GOOGLE_APPLICATION_CREDENTIALS || join(homedir(), '.config', 'aurora-play', 'reader.json'),
    packageName: env.AURORA_PLAY_PACKAGE || 'com.auroralights.app',
    defaultTrack: env.AURORA_PLAY_TRACK || 'internal',
    accessToken: env.GOOGLE_OAUTH_ACCESS_TOKEN || null,
  };
  if (!/^[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+$/.test(settings.packageName))
    throw new PlayError('AURORA_PLAY_PACKAGE must be an Android package name.');
  validateTrack(settings.defaultTrack);
  return settings;
}

export function validateTrack(track) {
  if (typeof track !== 'string' || !/^[A-Za-z0-9][A-Za-z0-9_.:-]{0,199}$/.test(track))
    throw new PlayError('Use an API track identifier, such as internal or production, not a Console URL.');
  return track;
}

export function validateVersion(code) {
  if (!Number.isSafeInteger(code) || code < 1 || code > 2100000000)
    throw new PlayError('version_code must be an integer between 1 and 2100000000.');
  return code;
}

function normalizeRelease(release, track) {
  if (!release || typeof release !== 'object' || Array.isArray(release))
    throw new PlayError('Google returned an unexpected release summary.');
  const artifacts = release.activeArtifacts ?? [];
  if (!Array.isArray(artifacts)) throw new PlayError('Google returned an unexpected artifact list.');
  const versions = artifacts.map(artifact => {
    let code = artifact?.versionCode;
    if (typeof code === 'string' && /^[0-9]+$/.test(code)) code = Number(code);
    return validateVersion(code);
  });
  const name = release.releaseName ?? '';
  const actualTrack = release.track ?? track;
  const state = release.releaseLifecycleState ?? 'RELEASE_LIFECYCLE_STATE_UNSPECIFIED';
  if (![name, actualTrack, state].every(value => typeof value === 'string') || actualTrack !== track)
    throw new PlayError('Google returned an unexpected release or a different track.');
  return { releaseName: name, track, versionCodes: [...new Set(versions)].sort((a, b) => a - b), releaseLifecycleState: state };
}

export class PlayClient {
  constructor(settings, fetchImpl = fetch) {
    this.settings = settings;
    this.fetchImpl = fetchImpl;
    this.cachedToken = null;
    this.tokenPromise = null;
  }

  async request(url, options, signal) {
    try {
      return await this.fetchImpl(url, {
        ...options, redirect: 'error',
        signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(30000)]) : AbortSignal.timeout(30000),
      });
    } catch {
      if (signal?.aborted) throw new PlayError('Request cancelled.');
      throw new PlayError('Could not reach Google. Check network/proxy access and operating-system certificate trust.');
    }
  }

  async getToken(signal) {
    if (this.settings.accessToken) return this.settings.accessToken;
    if (this.cachedToken && this.cachedToken.expiresAt > Date.now()) return this.cachedToken.token;
    // Authentication is shared, but individual release reads retain their own cancellation.
    if (!this.tokenPromise) this.tokenPromise = this.mintToken().finally(() => { this.tokenPromise = null; });
    return this.tokenPromise;
  }

  async mintToken() {
    let key;
    try {
      key = JSON.parse((await readFile(this.settings.credentialFile, 'utf8')).replace(/^\uFEFF/, ''));
    } catch {
      throw new PlayError('Could not read the service account JSON. Check GOOGLE_APPLICATION_CREDENTIALS and file access.');
    }
    if (key.type !== 'service_account' || typeof key.client_email !== 'string' ||
        typeof key.private_key !== 'string' || (key.token_uri && key.token_uri !== TOKEN_URL))
      throw new PlayError('Use the original Google service account JSON key with the Google OAuth token endpoint.');
    let assertion;
    try {
      const now = Math.floor(Date.now() / 1000);
      const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
      const header = { alg: 'RS256', typ: 'JWT' };
      if (typeof key.private_key_id === 'string') header.kid = key.private_key_id;
      const input = encode(header) + '.' + encode({
        iss: key.client_email, scope: SCOPE, aud: TOKEN_URL, iat: now, exp: now + 3600,
      });
      const privateKey = createPrivateKey(key.private_key);
      if (privateKey.asymmetricKeyType !== 'rsa') throw new Error('Not an RSA key');
      assertion = input + '.' + sign('RSA-SHA256', Buffer.from(input), privateKey).toString('base64url');
    } catch {
      throw new PlayError('The service account signing key is invalid. Download a valid JSON key from Google Cloud.');
    } finally {
      key = null;
    }
    const response = await this.request(TOKEN_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer', assertion }).toString(),
    });
    if (!response.ok) throw new PlayError('Google rejected service account authentication. Check the key is active and the system clock is correct.');
    let token;
    try { token = await response.json(); }
    catch { throw new PlayError('Google returned an invalid authentication response.'); }
    if (typeof token.access_token !== 'string' || !token.access_token ||
        !Number.isFinite(token.expires_in) || token.expires_in <= 60)
      throw new PlayError('Google returned an unexpected authentication response.');
    this.cachedToken = { token: token.access_token, expiresAt: Date.now() + (token.expires_in - 60) * 1000 };
    return token.access_token;
  }

  async getReleases(track = this.settings.defaultTrack, signal) {
    validateTrack(track);
    if (signal?.aborted) throw new PlayError('Request cancelled.');
    const token = await this.getToken(signal);
    if (signal?.aborted) throw new PlayError('Request cancelled.');
    const url = ROOT + '/applications/' + encodeURIComponent(this.settings.packageName) +
      '/tracks/' + encodeURIComponent(track) + '/releases';
    const response = await this.request(url, { method: 'GET', headers: { Authorization: 'Bearer ' + token } }, signal);
    if (!response.ok) {
      const errors = {
        401: 'Google rejected the access token. Retry authentication.',
        403: 'Access denied. Enable Google Play Android Developer API in the service account project and grant View app information (read-only) for this app in Play Console.',
        404: 'App or track was not found. Check the package and exact track API identifier.',
        429: 'Google Play API quota was reached. Retry later.',
      };
      if (response.status === 401) this.cachedToken = null;
      throw new PlayError(errors[response.status] || 'Google Play API returned HTTP ' + response.status + '.');
    }
    let payload;
    try { payload = await response.json(); }
    catch { throw new PlayError('Google returned an invalid JSON response.'); }
    if (!payload || typeof payload !== 'object' || Array.isArray(payload) ||
        (Object.hasOwn(payload, 'releases') && !Array.isArray(payload.releases)))
      throw new PlayError('Google returned an unexpected release list.');
    const releases = (payload.releases ?? []).map(item => normalizeRelease(item, track));
    const publishedCodes = releases.filter(item => item.releaseLifecycleState === PUBLISHED).flatMap(item => item.versionCodes);
    return {
      packageName: this.settings.packageName, track, checkedAtUtc: new Date().toISOString(),
      releases, highestPublishedVersionCode: publishedCodes.length ? Math.max(...publishedCodes) : null,
      limitations: LIMITATIONS,
    };
  }

  async findVersion(versionCode, track = this.settings.defaultTrack, signal) {
    validateVersion(versionCode);
    const result = await this.getReleases(track, signal);
    const releases = result.releases.filter(item => item.versionCodes.includes(versionCode));
    return { ...result, versionCode, found: releases.length > 0,
      publishedReleaseFound: releases.some(item => item.releaseLifecycleState === PUBLISHED), releases };
  }
}
