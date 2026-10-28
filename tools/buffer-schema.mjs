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
  if (result.errors?.length) throw new Error(result.errors.map((e) => e.message).join('; '));
  return result.data;
}

function describe(type) {
  if (!type) return 'unknown';
  if (type.kind === 'NON_NULL') return `${describe(type.ofType)}!`;
  if (type.kind === 'LIST') return `[${describe(type.ofType)}]`;
  return type.name || type.kind;
}

async function main() {
  const apiKey = process.env.BUFFER_API_KEY;
  const wanted = process.argv.slice(2);
  const targets = wanted.length ? wanted : ['EditPostInput', 'DeletePostInput'];

  const data = await bufferRequest(apiKey, `query IntrospectTypes {
    __schema { types { name kind inputFields { name type { kind name ofType { kind name ofType { kind name } } } } } } }
  }`);

  for (const name of targets) {
    const type = (data?.__schema?.types || []).find((t) => t.name === name);
    if (!type) { console.log(`TYPE ${name}: NOT FOUND`); continue; }
    console.log(`\nTYPE ${name} (${type.kind})`);
    for (const field of type.inputFields || []) {
      console.log(`  ${field.name}: ${describe(field.type)}`);
    }
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((e) => { console.error('Failed: ' + e.message); process.exitCode = 1; });
}
