import { pathToFileURL } from 'node:url';
import { verifyChannelMappings } from './verify-buffer-channels.mjs';

const BUFFER_API_URL = 'https://api.buffer.com';
const POST_LIMITS = { x: 280, threads: 500 };
const CHANNEL_ENV = {
  x: 'BUFFER_X_CHANNEL_ID',
  threads: 'BUFFER_THREADS_CHANNEL_ID',
};

function requiredEnvironment(name) {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} is missing.`);
  return value;
}

function readScheduleRequest() {
  const dueAt = requiredEnvironment('BUFFER_DUE_AT_UTC');
  const dueTime = Date.parse(dueAt);
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/.test(dueAt) || !Number.isFinite(dueTime)) {
    throw new Error('BUFFER_DUE_AT_UTC must be an ISO 8601 UTC timestamp with milliseconds.');
  }
  if (dueTime < Date.now() + 5 * 60 * 1000) {
    throw new Error('The requested posting time must be at least five minutes in the future.');
  }

  let posts;
  try {
    posts = JSON.parse(requiredEnvironment('BUFFER_POSTS_JSON'));
  } catch {
    throw new Error('BUFFER_POSTS_JSON must contain valid JSON.');
  }
  if (!Array.isArray(posts) || posts.length !== 2) {
    throw new Error('Provide exactly one post for X and one for Threads.');
  }

  const byService = new Map();
  for (const post of posts) {
    if (!post || !['x', 'threads'].includes(post.service) || typeof post.text !== 'string' || !post.text.trim()) {
      throw new Error('Each post must include a supported service and non-empty text.');
    }
    if (byService.has(post.service)) throw new Error(`Only one ${post.service} post can be scheduled at a time.`);
    if (post.text.length > POST_LIMITS[post.service]) {
      throw new Error(`The ${post.service} post exceeds the ${POST_LIMITS[post.service]} character limit.`);
    }
    byService.set(post.service, post.text.trim());
  }
  if (!byService.has('x') || !byService.has('threads')) {
    throw new Error('Provide exactly one post for X and one for Threads.');
  }

  return { dueAt, dueTime, posts: byService };
}

async function bufferRequest(apiKey, query) {
  let response;
  try {
    response = await fetch(BUFFER_API_URL, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${apiKey}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ query }),
      signal: AbortSignal.timeout(30000),
    });
  } catch {
    throw new Error('Could not reach the Buffer API.');
  }

  let result;
  try {
    result = await response.json();
  } catch {
    throw new Error(`Buffer API returned HTTP ${response.status} with an invalid response.`);
  }
  if (!response.ok) throw new Error(`Buffer API returned HTTP ${response.status}.`);
  if (result.errors?.length) throw new Error(`Buffer API rejected a query (${result.errors.length} GraphQL error(s)).`);
  return result.data;
}

async function getChannels(apiKey) {
  const account = await bufferRequest(apiKey, 'query { account { organizations { id } } }');
  const organizations = account?.account?.organizations;
  if (!Array.isArray(organizations) || organizations.length === 0) {
    throw new Error('No Buffer organizations were returned for this API key.');
  }

  const channelsByOrganization = [];
  for (const organization of organizations) {
    const organizationId = organization?.id;
    if (!organizationId) continue;
    const query = `query { channels(input: { organizationId: ${JSON.stringify(organizationId)} }) { id service isDisconnected } }`;
    const data = await bufferRequest(apiKey, query);
    channelsByOrganization.push({
      organizationId,
      channels: (data?.channels || []).map((channel) => ({ ...channel, organizationId })),
    });
  }
  return channelsByOrganization;
}

async function getScheduledPosts(apiKey, organizationId, channelIds) {
  const posts = [];
  let after = null;
  let hasNextPage = true;

  while (hasNextPage) {
    const query = `query GetScheduledPosts {
      posts(first: 100, after: ${after === null ? 'null' : JSON.stringify(after)}, input: {
        organizationId: ${JSON.stringify(organizationId)}
        filter: { status: [scheduled], channelIds: ${JSON.stringify(channelIds)} }
      }) {
        edges { node { id text dueAt channelId } }
        pageInfo { endCursor hasNextPage }
      }
    }`;
    const data = await bufferRequest(apiKey, query);
    const result = data?.posts;
    if (!result || !Array.isArray(result.edges)) throw new Error('Buffer did not return scheduled-post data.');
    posts.push(...result.edges.map((edge) => edge.node).filter(Boolean));
    after = result.pageInfo?.endCursor || null;
    hasNextPage = Boolean(result.pageInfo?.hasNextPage);
    if (hasNextPage && !after) throw new Error('Buffer returned an incomplete scheduled-post page.');
  }
  return posts;
}

async function createScheduledPost(apiKey, channelId, service, text, dueAt) {
  const query = `mutation CreateScheduledPost {
    createPost(input: {
      text: ${JSON.stringify(text)}
      channelId: ${JSON.stringify(channelId)}
      schedulingType: automatic
      mode: customScheduled
      dueAt: ${JSON.stringify(dueAt)}
    }) {
      ... on PostActionSuccess { post { id status dueAt channelId } }
      ... on MutationError { message }
    }
  }`;
  const data = await bufferRequest(apiKey, query);
  const result = data?.createPost;
  if (!result?.post?.id) throw new Error(`Buffer did not confirm the scheduled ${service} post.`);
  if (result.post.channelId !== channelId || Date.parse(result.post.dueAt) !== Date.parse(dueAt)) {
    throw new Error(`Buffer returned an unexpected schedule for the ${service} post.`);
  }
}

export function formatPacificTime(dueAt) {
  return new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/Los_Angeles',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'short',
  }).format(new Date(dueAt));
}

async function main() {
  const apiKey = requiredEnvironment('BUFFER_API_KEY');
  const xChannelId = requiredEnvironment(CHANNEL_ENV.x);
  const threadsChannelId = requiredEnvironment(CHANNEL_ENV.threads);
  const { dueAt, dueTime, posts } = readScheduleRequest();

  const organizations = await getChannels(apiKey);
  const channels = organizations.flatMap((organization) => organization.channels);
  verifyChannelMappings(channels, xChannelId, threadsChannelId);

  const targetChannels = new Map([
    ['x', xChannelId],
    ['threads', threadsChannelId],
  ]);
  const targetIdsByOrganization = new Map();
  for (const channelId of targetChannels.values()) {
    const channel = channels.find((candidate) => candidate.id === channelId);
    const ids = targetIdsByOrganization.get(channel.organizationId) || [];
    ids.push(channelId);
    targetIdsByOrganization.set(channel.organizationId, ids);
  }

  const scheduledPosts = [];
  for (const [organizationId, channelIds] of targetIdsByOrganization) {
    scheduledPosts.push(...await getScheduledPosts(apiKey, organizationId, channelIds));
  }

  const alreadyScheduled = new Set();
  for (const [service, channelId] of targetChannels) {
    const match = scheduledPosts.find((post) => post.channelId === channelId && Date.parse(post.dueAt) === dueTime);
    if (match) {
      alreadyScheduled.add(service);
      console.log(`${service === 'x' ? 'X' : 'Threads'} is already scheduled for ${formatPacificTime(dueAt)}; leaving it in place.`);
    }
  }

  for (const [service, channelId] of targetChannels) {
    if (alreadyScheduled.has(service)) continue;
    await createScheduledPost(apiKey, channelId, service, posts.get(service), dueAt);
    console.log(`Scheduled ${service === 'x' ? 'X' : 'Threads'} for ${formatPacificTime(dueAt)}.`);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(`Buffer scheduling failed: ${error.message}`);
    process.exitCode = 1;
  });
}
