import { readFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';

const BUFFER_API_URL = 'https://api.buffer.com';
const X_LIMIT = 280;
const THREADS_LIMIT = 500;
const PUBLIC_SECTION = /^#{1,6}\s*promo notes?\s*$/i;
const DEFAULT_PRODUCT_URL = 'https://aurora.idlehandz.com';
const DEFAULT_SUPPORT_URL = 'https://idlehandz.com/support/';

function cleanLine(value) {
  return String(value || '')
    .replace(/<[^>]*>/g, '')
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '$1 ($2)')
    .replace(/[`*~]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function unique(values) {
  return [...new Set(values.map(cleanLine).filter(Boolean))];
}

function extractReleaseNotes(body = '') {
  const fragments = [];
  let inCodeBlock = false;

  for (const rawLine of String(body).split(/\r?\n/)) {
    const line = rawLine.trim();
    if (line.startsWith('```')) {
      inCodeBlock = !inCodeBlock;
      continue;
    }
    if (inCodeBlock || !line || line.startsWith('<!--') || /^#{1,6}\s/.test(line)) continue;

    const bullet = line.match(/^(?:[-*+]\s+|\d+[.)]\s+)(.+)$/);
    if (bullet) fragments.push(bullet[1]);
  }

  return fragments;
}

function extractPublicPrNotes(body = '') {
  const fragments = [];
  let inPublicSection = false;
  let inCodeBlock = false;

  for (const rawLine of String(body).split(/\r?\n/)) {
    const line = rawLine.trim();
    if (line.startsWith('```')) {
      inCodeBlock = !inCodeBlock;
      continue;
    }
    if (inCodeBlock) continue;

    if (/^#{1,6}\s/.test(line)) {
      inPublicSection = PUBLIC_SECTION.test(line);
      continue;
    }
    if (!inPublicSection || !line || line.startsWith('<!--')) continue;

    const bullet = line.match(/^(?:[-*+]\s+|\d+[.)]\s+)(.+)$/);
    fragments.push(bullet ? bullet[1] : line);
  }

  return fragments;
}

function truncate(text, limit) {
  if (text.length <= limit) return text;
  if (limit < 2) return text.slice(0, limit);
  return `${text.slice(0, limit - 1).trimEnd()}…`;
}

function cleanUrl(value) {
  return String(value || '').replace(/[\r\n]/g, '').trim();
}

function buildChannelText({ projectName, releaseName, fragments, productUrl, supportUrl }, channel) {
  const isX = channel === 'x';
  const heading = isX ? `${projectName} — ${releaseName}` : `${projectName} update: ${releaseName}`;
  const body = isX ? fragments.map((fragment) => `• ${fragment}`).join('\n') : fragments.join(' ');
  const links = [
    productUrl ? `${isX ? '' : 'Try it: '}${productUrl}` : '',
    supportUrl ? `${isX ? 'Support' : 'Support on Ko-fi or Patreon'}: ${supportUrl}` : '',
  ].filter(Boolean).join('\n');
  const limit = isX ? X_LIMIT : THREADS_LIMIT;
  const linkBlock = links ? `\n\n${links}` : '';
  const fittedHeading = truncate(heading, Math.max(8, limit - linkBlock.length - 4));
  const fixed = `${fittedHeading}${linkBlock}`;
  const bodyLimit = Math.max(0, limit - fixed.length - (body ? 2 : 0));
  const content = body && bodyLimit > 1 ? `\n\n${truncate(body, bodyLimit)}` : '';

  return `${fittedHeading}${content}${linkBlock}`;
}

export function buildDrafts(input) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) {
    throw new Error('Input must be a JSON object.');
  }

  const release = input.release || {};
  const projectName = cleanLine(input.projectName || 'Aurora Lights');
  const releaseName = cleanLine(release.name || release.tagName || 'New update');
  const fragments = unique([
    ...extractReleaseNotes(release.body),
    ...(Array.isArray(input.pullRequests)
      ? input.pullRequests.flatMap((pr) => pr && typeof pr === 'object' ? extractPublicPrNotes(pr.body) : [])
      : []),
    ...(Array.isArray(input.fragments) ? input.fragments.filter((fragment) => typeof fragment === 'string') : []),
  ]);

  if (!fragments.length) return [];

  const shared = {
    projectName,
    releaseName,
    fragments,
    productUrl: cleanUrl(input.productUrl || DEFAULT_PRODUCT_URL),
    supportUrl: cleanUrl(input.supportUrl || DEFAULT_SUPPORT_URL),
  };

  return [
    { service: 'x', text: buildChannelText(shared, 'x') },
    { service: 'threads', text: buildChannelText(shared, 'threads') },
  ];
}

function githubEventInput(event) {
  if (event.release) {
    return {
      release: {
        name: event.release.name || event.release.tag_name,
        body: event.release.body || '',
        url: event.release.html_url,
      },
    };
  }

  if (event.pull_request) {
    if (event.action === 'closed' && !event.pull_request.merged) {
      throw new Error('This pull request was not merged.');
    }
    return {
      release: {
        name: `PR #${event.pull_request.number}: ${event.pull_request.title}`,
        url: event.pull_request.html_url,
      },
      pullRequests: [event.pull_request],
    };
  }

  throw new Error('GitHub event must contain a release or pull_request payload.');
}

async function bufferRequest(query) {
  const apiKey = process.env.BUFFER_API_KEY;
  if (!apiKey) throw new Error('Set BUFFER_API_KEY before using the Buffer API.');

  const response = await fetch(BUFFER_API_URL, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ query }),
    signal: AbortSignal.timeout(30000),
  });
  const result = await response.json();

  if (!response.ok) throw new Error(`Buffer API returned HTTP ${response.status}.`);
  if (result.errors?.length) throw new Error(result.errors.map((error) => error.message).join('; '));
  return result.data;
}

async function listChannels() {
  const data = await bufferRequest('query { account { organizations { id name } } }');
  const organizations = data?.account?.organizations || [];

  for (const organization of organizations) {
    const query = `query { channels(input: { organizationId: ${JSON.stringify(organization.id)} }) { id name service isDisconnected } }`;
    const channelData = await bufferRequest(query);
    for (const channel of channelData?.channels || []) {
      console.log(`${organization.name}\t${channel.service}\t${channel.name}\t${channel.isDisconnected ? 'disconnected' : 'connected'}\t${channel.id}`);
    }
  }
}

async function createBufferDraft(channelId, text) {
  const query = `mutation CreateDraftPost {
    createPost(input: {
      text: ${JSON.stringify(text)}
      channelId: ${JSON.stringify(channelId)}
      schedulingType: automatic
      mode: addToQueue
      saveToDraft: true
    }) {
      ... on PostActionSuccess { post { id status } }
      ... on MutationError { message }
    }
  }`;
  const data = await bufferRequest(query);
  const result = data?.createPost;
  if (result?.message) throw new Error(result.message);
  if (!result?.post?.id) throw new Error('Buffer did not return a created draft.');
  return result.post;
}

async function main(args) {
  if (args.includes('--list-channels')) {
    await listChannels();
    return;
  }

  const inputPath = args.find((arg) => !arg.startsWith('--'));
  const eventMode = args.includes('--github-event');
  if (!inputPath && !eventMode) {
    throw new Error('Usage: node tools/create-buffer-promo-drafts.mjs <input.json> [--buffer] | --github-event [--buffer] | --list-channels');
  }

  let input;
  if (eventMode) {
    const eventPath = process.env.GITHUB_EVENT_PATH;
    if (!eventPath) throw new Error('Set GITHUB_EVENT_PATH to a GitHub release or pull request event payload.');
    input = githubEventInput(JSON.parse(await readFile(eventPath, 'utf8')));
  } else {
    input = JSON.parse(await readFile(inputPath, 'utf8'));
  }
  const drafts = buildDrafts(input);
  if (!drafts.length) {
    console.log('No public update fragments found; no Buffer drafts created.');
    return;
  }

  if (!args.includes('--buffer')) {
    console.log(JSON.stringify(drafts, null, 2));
    return;
  }

  const channelIds = {
    x: process.env.BUFFER_X_CHANNEL_ID,
    threads: process.env.BUFFER_THREADS_CHANNEL_ID,
  };
  for (const draft of drafts) {
    if (!channelIds[draft.service]) {
      throw new Error(`Set BUFFER_${draft.service.toUpperCase()}_CHANNEL_ID before creating drafts.`);
    }
  }

  const created = [];

  for (const draft of drafts) {
    const channelId = channelIds[draft.service];
    const post = await createBufferDraft(channelId, draft.text);
    created.push({ service: draft.service, id: post.id, status: post.status });
  }

  console.log(JSON.stringify(created, null, 2));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.argv.slice(2)).catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
