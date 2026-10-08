import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/ui-layout',
  globalSetup: './tests/ui-layout/global-setup.ts',
  timeout: 15_000,
  workers: 1,
  reporter: 'list',
  use: {
    browserName: 'chromium',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure'
  }
});
