import { pathToFileURL } from 'node:url';

const BUFFER_API_URL = 'https://api.buffer.com';

async function bufferRequest(apiKey, query) {
  const response = await fetch(BUFFER_API_URL, {
    method: 'POST',
    headers: { Authorization: `Bearer ${apiKey}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ query }),
    signal: AbortSignal.timeout(30000),
  });
  const result = await response.json();
  if (!response.ok) throw new Error(`Buffer API returned HTTP ${response.status}.`);
  if (result.errors?.length) {
    throw new Error(result.errors.map((e) => `${e.message}${e.extensions?.code ? ` [${e.extensions.code}]` : ''}`).join('; '));
  }
  return result.data;
}

async function main() {
  const apiKey = process.env.BUFFER_API_KEY;
  const postId = process.argv[2];
  const text = process.argv[3];

  const query = `mutation EditPost {
    editPost(input: { id: ${JSON.stringify(postId)}, text: ${JSON.stringify(text)} }) {
      ... on PostActionSuccess { post { id text status dueAt channelId } }
      ... on MutationError { message }
    }
  }`;
  const data = await bufferRequest(apiKey, query);
  console.log(JSON.stringify(data, null, 2));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((e) => { console.error('Failed: ' + e.message); process.exitCode = 1; });
}
