import { pathToFileURL } from 'node:url';

const BUFFER_API_URL = 'https://api.buffer.com';

function requiredEnvironment(name) {
  const value = process.env[name];
  if (!value || !value.trim()) throw new Error(`${name} is missing.`);
  return value;
}

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
  const apiKey = requiredEnvironment('BUFFER_API_KEY');
  const postId = requiredEnvironment('BUFFER_EDIT_POST_ID');
  const text = requiredEnvironment('BUFFER_EDIT_POST_TEXT');

  const query = `mutation EditPost {
    editPost(input: { id: ${JSON.stringify(postId)}, text: ${JSON.stringify(text)} }) {
      ... on PostActionSuccess { post { id text status dueAt channelId } }
      ... on MutationError { message }
    }
  }`;
  const data = await bufferRequest(apiKey, query);
  const result = data?.editPost;
  if (result?.message) throw new Error(result.message);
  if (!result?.post?.id) throw new Error('Buffer did not confirm the edit.');
  if (result.post.text !== text) throw new Error('Buffer stored a different text than requested.');

  console.log(`Edited post ${result.post.id} (${result.post.status}, due ${result.post.dueAt}, ${result.post.text.length} chars).`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((e) => { console.error('Failed: ' + e.message); process.exitCode = 1; });
}
