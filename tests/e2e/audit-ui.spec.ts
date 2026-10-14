import { expect, test } from '@playwright/test';
import path from 'node:path';

test.afterEach(async ({ page }, testInfo) => {
  if (page.isClosed() || testInfo.status === 'skipped' || page.url() === 'about:blank') return;
  await page.locator('.web-nav-drawer a[href="/import"]').click();
  await page.getByRole('button', { name: 'Clear Session Imports', exact: true }).click();
  await expect(page.locator('.status-banner.success')).toContainText('Session workspace cleared.');
});

const legacyMonkFixture = path.resolve(
  process.cwd(), 'Aurora.Tests/Fixtures/Characters/legacy-edited-arilith.dnd5e'
);

test('Character search survives selection and can be cleared', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name === 'chromium-mobile', 'Covered on desktop.');
  await page.goto('/import', { waitUntil: 'networkidle' });
  await page.locator('#phase0-upload').setInputFiles(legacyMonkFixture);
  await expect(page.locator('.status-banner.success')).toContainText('Imported 1 file');
  await page.locator('.web-nav-drawer a[href="/"]').click();
  const search = page.locator('.character-browser-search');
  await search.fill('Arilith');
  await expect(page.locator('.character-browser-card')).toHaveCount(1);
  await page.locator('.character-browser-card').click();
  await expect(search).toHaveValue('Arilith');
  await expect(page.locator('.character-browser-card')).toHaveCount(1);
  await search.fill('not a matching character');
  await expect(page.locator('.character-browser-card')).toHaveCount(0);
  await search.fill('');
  await expect(page.locator('.character-browser-card')).toHaveCount(1);
});

test('Completed standard array swaps and persists the two affected scores', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name === 'chromium-mobile', 'Covered on desktop.');
  test.slow();
  await page.goto('/', { waitUntil: 'networkidle' });
  await page.getByLabel('Character Name', { exact: true }).fill('Array Swap Regression');
  await page.getByRole('button', { name: 'Create New Character', exact: true }).click();
  await expect(page).toHaveURL(/\/build$/, { timeout: 60_000 });
  await page.getByRole('button', { name: 'Standard Array', exact: true }).click();
  const scores = page.locator('.ability-array-select-sm');
  await expect(scores).toHaveCount(6);
  for (const [index, value] of ['15', '14', '13', '12', '10', '8'].entries()) {
    await scores.nth(index).selectOption(value);
    await expect(scores.nth(index)).toBeEnabled();
  }
  await scores.nth(0).selectOption('14');
  await expect(scores.nth(0)).toHaveValue('14');
  await expect(scores.nth(1)).toHaveValue('15');
  await expect(scores.nth(0)).toBeEnabled();
  await page.locator('.web-nav-drawer a[href="/workspace"]').click();
  await page.locator('.web-nav-drawer a[href="/build"]').click();
  await page.getByRole('button', { name: 'Standard Array', exact: true }).click();
  await expect(scores.nth(0)).toHaveValue('14');
  await expect(scores.nth(1)).toHaveValue('15');
  await expect(page.locator('#blazor-error-ui')).toBeHidden();
});
