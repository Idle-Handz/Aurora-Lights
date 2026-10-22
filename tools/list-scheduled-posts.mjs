import { pathToFileURL } from 'node:url';

const BUFFER_API_URL = 'https://api.buffer.com';

function requiredEnvironment(name) {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} is missing.`);
  return value;
}

async function bufferRequest(apiKey, query) {
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
  if (result.errors?.length) throw new Error(`Buffer API rejected a query (${result.errors.length} GraphQL error(s)).`);
  return result.data;
}

function formatPacificTime(dueAt) {
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
        edges { node { id text dueAt channelId channelService } }
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

async function main() {
  const apiKey = requiredEnvironment('BUFFER_API_KEY');
  const channelIds = [
    requiredEnvironment('BUFFER_X_CHANNEL_ID'),
    requiredEnvironment('BUFFER_THREADS_CHANNEL_ID'),
  ];

  const account = await bufferRequest(apiKey, 'query { account { organizations { id } } }');
  const organizations = account?.account?.organizations;
  if (!Array.isArray(organizations) || organizations.length === 0) {
    throw new Error('No Buffer organizations were returned for this API key.');
  }

  const posts = [];
  for (const organization of organizations) {
    if (!organization?.id) continue;
    posts.push(...await getScheduledPosts(apiKey, organization.id, channelIds));
  }

  if (posts.length === 0) {
    console.log('No scheduled posts on the X and Threads channels.');
    return;
  }

  for (const post of posts.sort((left, right) => Date.parse(left.dueAt) - Date.parse(right.dueAt))) {
    console.log(`id=${post.id} service=${post.channelService} due=${formatPacificTime(post.dueAt)} chars=${post.text.length}`);
    console.log(post.text);
    console.log();
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(`Could not list scheduled Buffer posts: ${error.message}`);
    process.exitCode = 1;
  });
}
