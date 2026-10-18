import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const output = 'tests/ui-layout/FixtureRenderer/bin/Debug/net10.0';
const read = (file: string) => readFileSync(path.resolve(output, file), 'utf8');

for (const width of [1280, 390]) {
  test(`creature management controls and details remain reachable at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.setContent(`<!doctype html><html><head><meta name="viewport" content="width=device-width, initial-scale=1">
      <style>${read('MudBlazor.min.css')}</style><style>${read('app.css')}</style>
      <style>${read('CreatureManagementCard.css')}</style>
      <style>body { padding:16px; font-family:Segoe UI,sans-serif; background:#1e1e2e; color:#cdd6f4;
        --mud-palette-divider:#45475a; --mud-palette-surface:#313244; --mud-palette-text-secondary:#a6adc8;
        --mud-palette-error:#f38ba8; --mud-palette-success:#a6e3a1; --mud-palette-primary:#89b4fa; }</style>
      </head><body>${read('creature.html')}</body></html>`);
    for (const name of ['Finish choices', 'Edit profile', 'Change portrait', 'Damage', 'Heal', 'Grant temp HP', 'Short rest', 'Long rest']) {
      const button = page.getByRole('button', { name, exact: true });
      await button.scrollIntoViewIfNeeded();
      await button.click({ trial: true });
    }
    await expect(page.getByRole('img')).toBeVisible();
    expect(await page.getByRole('img').evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0)).toBeTruthy();
    await page.getByLabel('Notes', { exact: true }).fill('Individual creature notes');
    await page.getByLabel('Notes', { exact: true }).scrollIntoViewIfNeeded();
    await expect(page.getByRole('button', { name: 'Save notes', exact: true })).toBeVisible();
    await page.getByLabel('Add condition').selectOption('Prone');
    await page.locator('summary').click();
    const details = page.locator('.creature-details-grid');
    await details.scrollIntoViewIfNeeded();
    await expect(details).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
    await page.evaluate(() => { window.scrollTo(0, 0); document.body.scrollTop = 0; });
    await page.screenshot({ path: `test-results/creature-management-${width}.png`, fullPage: true });
  });
}
