import assert from 'node:assert/strict';
import test from 'node:test';
import { formatPacificTime } from './schedule-buffer-posts.mjs';

test('formats a scheduled time in Pacific time', () => {
  assert.equal(formatPacificTime('2026-10-23T16:00:00.000Z'), 'Oct 23, 2026, 9:00 AM PDT');
});

test('formats a winter time in Pacific time', () => {
  assert.equal(formatPacificTime('2026-01-15T17:00:00.000Z'), 'Jan 15, 2026, 9:00 AM PST');
});
