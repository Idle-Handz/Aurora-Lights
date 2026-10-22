import assert from 'node:assert/strict';
import test from 'node:test';
import { verifyChannelMappings } from './verify-buffer-channels.mjs';

const channels = [
  { id: 'x-channel', service: 'twitter', isDisconnected: false },
  { id: 'threads-channel', service: 'threads', isDisconnected: false },
];

test('accepts connected X/Twitter and Threads channel mappings', () => {
  assert.deepEqual(verifyChannelMappings(channels, 'x-channel', 'threads-channel'), {
    xService: 'twitter',
    threadsService: 'threads',
  });
});

test('rejects an X ID mapped to a different service', () => {
  assert.throws(
    () => verifyChannelMappings(channels, 'threads-channel', 'threads-channel'),
    /X\/Twitter channel maps to 'threads'/,
  );
});

test('rejects a channel ID not found in Buffer', () => {
  assert.throws(
    () => verifyChannelMappings(channels, 'missing-channel', 'threads-channel'),
    /X\/Twitter channel ID was not found/,
  );
});

test('rejects a disconnected channel', () => {
  assert.throws(
    () => verifyChannelMappings([{ ...channels[0], isDisconnected: true }, channels[1]], 'x-channel', 'threads-channel'),
    /X\/Twitter channel is disconnected/,
  );
});
