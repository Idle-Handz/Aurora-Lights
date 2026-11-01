import { pathToFileURL } from 'node:url';

const BUFFER_API_URL = 'https://api.buffer.com';

export function verifyChannelMappings(channels, xChannelId, threadsChannelId) {
  const verify = (label, channelId, expectedServices) => {
    if (!channelId) throw new Error(`${label} channel ID is missing.`);

    const channel = channels.find((candidate) => candidate.id === channelId);
    if (!channel) throw new Error(`${label} channel ID was not found in Buffer.`);
    if (channel.isDisconnected) throw new Error(`${label} channel is disconnected in Buffer.`);

    const service = String(channel.service || '').toLowerCase();
    if (!expectedServices.includes(service)) {
      throw new Error(`${label} channel maps to '${service || 'unknown'}', not the expected service.`);
    }
    return service;
  };

  return {
    xService: verify('X/Twitter', xChannelId, ['twitter', 'x']),
    threadsService: verify('Threads', threadsChannelId, ['threads']),
  };
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
  if (result.errors?.length) {
    throw new Error(`Buffer API rejected a query (${result.errors.length} GraphQL error(s)).`);
  }
  return result.data;
}

async function main() {
  const apiKey = process.env.BUFFER_API_KEY;
  if (!apiKey) throw new Error('BUFFER_API_KEY is missing.');

  const xChannelId = process.env.BUFFER_X_CHANNEL_ID;
  const threadsChannelId = process.env.BUFFER_THREADS_CHANNEL_ID;
  if (!xChannelId || !threadsChannelId) throw new Error('A required Buffer channel ID is missing.');

  const account = await bufferRequest(apiKey, 'query { account { organizations { id } } }');
  const organizations = account?.account?.organizations;
  if (!Array.isArray(organizations) || organizations.length === 0) {
    throw new Error('No Buffer organizations were returned for this API key.');
  }

  const channels = [];
  for (const organization of organizations) {
    const organizationId = JSON.stringify(organization.id);
    const query = `query { channels(input: { organizationId: ${organizationId} }) { id service isDisconnected } }`;
    const data = await bufferRequest(apiKey, query);
    channels.push(...(data?.channels || []));
  }

  const { xService, threadsService } = verifyChannelMappings(channels, xChannelId, threadsChannelId);
  console.log(`Buffer API key accepted; X/Twitter channel is connected (${xService}).`);
  console.log(`Threads channel is connected (${threadsService}).`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(`Buffer channel check failed: ${error.message}`);
    process.exitCode = 1;
  });
}
