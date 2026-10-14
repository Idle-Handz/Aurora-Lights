import { defineConfig } from '@playwright/test';

// These tests use the shipped JavaScript directly and need no .NET fixture host.
export default defineConfig({
    testDir: './ui-layout',
    testMatch: 'page-focus.spec.ts',
    timeout: 15_000,
    workers: 1,
    reporter: 'list',
    outputDir: '../artifacts/page-focus-tests',
    use: { browserName: 'chromium' },
});
