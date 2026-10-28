import { expect, test, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const output = 'tests/ui-layout/FixtureRenderer/bin/Debug/net10.0';
const read = (file: string) => readFileSync(path.resolve(output, file), 'utf8');

async function showPicker(page: Page) {
  await page.setContent(`<!doctype html><html><head>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>${read('MudBlazor.min.css')}</style><style>${read('app.css')}</style>
    <style>${read('RichDescription.css')}</style>
    </head><body>${read('picker.html')}</body></html>`);
}

test('dragging the picker corner enlarges both lists and details while keeping actions visible', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await showPicker(page);
  const dialog = page.locator('.picker-dialog');
  await dialog.getByRole('button', { name: 'Select', exact: true }).click({ trial: true });
  const list = dialog.locator('.picker-list');
  const details = dialog.locator('.picker-detail');
  const before = (await dialog.boundingBox())!;
  const listHeight = await list.evaluate(el => el.clientHeight);
  const detailHeight = await details.evaluate(el => el.clientHeight);

  await page.mouse.move(before.x + before.width - 3, before.y + before.height - 3);
  await page.mouse.down();
  await page.mouse.move(before.x + before.width + 120, before.y + before.height + 100, { steps: 12 });
  await page.mouse.up();

  await expect.poll(async () => (await dialog.boundingBox())!.width).toBeGreaterThan(before.width + 80);
  await expect.poll(async () => (await list.evaluate(el => el.clientHeight))).toBeGreaterThan(listHeight + 80);
  expect(await details.evaluate(el => el.clientHeight)).toBeGreaterThan(detailHeight + 80);
  expect(await details.locator('.rich-description').evaluate(el => el.scrollHeight - el.clientHeight),
    'The whole preview scrolls, not a shrunken description inside it').toBeLessThanOrEqual(1);
  await expect(dialog.getByRole('button', { name: 'Select', exact: true })).toBeInViewport({ ratio: 1 });
  await expect(dialog.getByRole('button', { name: 'Cancel', exact: true })).toBeInViewport({ ratio: 1 });
  await details.locator('p').last().scrollIntoViewIfNeeded();
  // A scroll boundary can round by a fraction of a CSS pixel.
  await expect(details.locator('p').last()).toBeInViewport({ ratio: 0.99 });
  await list.locator('.picker-item').last().scrollIntoViewIfNeeded();
  await list.locator('.picker-item').last().click({ trial: true });
  await page.screenshot({ path: 'test-results/picker-resized.png' });
});

test('picker remains usable on a narrow screen without a resize handle', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await showPicker(page);
  const dialog = page.locator('.picker-dialog');
  await expect(dialog).toHaveCSS('resize', 'none');
  await expect(dialog.locator('.picker-resize-hint')).toBeHidden();
  const lastFeature = dialog.locator('.picker-detail p').last();
  await lastFeature.scrollIntoViewIfNeeded();
  await expect(lastFeature).toBeInViewport({ ratio: 1 });
  await dialog.getByRole('button', { name: 'Select', exact: true }).click({ trial: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
});
