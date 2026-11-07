import { expect, test, type Page } from '@playwright/test';
import path from 'node:path';

declare global {
  interface Window {
    AuroraBack: { dismissOverlay(): boolean };
  }
}

async function showOverlayHost(page: Page, content: string) {
  await page.setViewportSize({ width: 360, height: 640 });
  // This host tests the production JS hit-testing contract. Native Android Back
  // dispatch and rendered MudBlazor overlays are checked separately on-device.
  await page.setContent(`<!doctype html><html><head><style>
    body { margin:0 }
    .mud-overlay, .mobile-more-backdrop, .shop-backdrop { position:fixed; inset:0 }
    .backdrop { position:absolute; inset:0 }
    .mud-dialog { position:fixed; inset:25%; background:white; z-index:20 }
  </style></head><body>${content}</body></html>`);
  await page.addScriptTag({ path: path.resolve('Aurora.Reflections/wwwroot/shortcuts.js') });
}

const dismissOverlay = (page: Page) => page.evaluate(() => window.AuroraBack.dismissOverlay());

test('Back dismisses shop item details before leaving the shop page', async ({ page }) => {
  await showOverlayHost(page, `
    <button class="shop-backdrop" style="z-index:10" aria-label="Close details"></button>
    <aside class="shop-detail" style="position:fixed;inset:25%;z-index:20;background:white">Club checkout</aside>`);
  await page.locator('.shop-backdrop').evaluate(el => el.addEventListener('click', () => {
    document.querySelector('.shop-detail')!.remove();
    el.remove();
  }));

  expect(await dismissOverlay(page)).toBe(true);
  await expect(page.locator('.shop-detail')).toHaveCount(0);
  expect(await dismissOverlay(page)).toBe(false);
});

test('Back closes the top menu before the dialog beneath it', async ({ page }) => {
  await showOverlayHost(page, `
    <div id="menu-overlay" class="mud-overlay" style="z-index:30"><div class="backdrop"></div></div>
    <div id="dialog-overlay" class="mud-overlay" style="z-index:10"><div class="backdrop"></div></div>
    <div class="mud-dialog">Dialog stays open while its menu closes</div>`);
  // Put the menu before the dialog in DOM order so only actual stacking can
  // choose the correct overlay. Click handlers stand in for component policy.
  await page.evaluate(() => {
    document.querySelector('#menu-overlay')!.addEventListener('click', () => {
      document.querySelector('#menu-overlay')!.remove();
    });
    document.querySelector('#dialog-overlay')!.addEventListener('click', () => {
      document.querySelector('#dialog-overlay')!.remove();
      document.querySelector('.mud-dialog')!.remove();
    });
  });

  expect(await dismissOverlay(page)).toBe(true);
  await expect(page.locator('#menu-overlay')).toHaveCount(0);
  await expect(page.locator('.mud-dialog')).toBeVisible();
  await expect(page.locator('#dialog-overlay')).toHaveCount(1);
  expect(await dismissOverlay(page)).toBe(true);
  await expect(page.locator('.mud-dialog')).toHaveCount(0);
  expect(await dismissOverlay(page)).toBe(false);
});

test('Back respects a dialog that does not allow backdrop dismissal', async ({ page }) => {
  await showOverlayHost(page, `
    <div class="mud-overlay" style="z-index:10"><div class="backdrop"></div></div>
    <div class="mud-dialog">Required decision</div>`);
  await page.locator('.mud-overlay').evaluate(el => el.addEventListener('click', () => {
    el.setAttribute('data-backdrop-clicked', 'true');
  }));

  expect(await dismissOverlay(page)).toBe(true);
  await expect(page.locator('.mud-overlay')).toHaveAttribute('data-backdrop-clicked', 'true');
  await expect(page.locator('.mud-dialog')).toBeVisible();
  // A visible modal must still consume Back if it has no backdrop at all.
  await page.locator('.mud-overlay').evaluate(el => el.remove());
  expect(await dismissOverlay(page)).toBe(true);
  await expect(page.locator('.mud-dialog')).toBeVisible();
});

test('Back closes the mobile More panel through its backdrop handler', async ({ page }) => {
  await showOverlayHost(page, `
    <div class="mobile-more-backdrop" style="z-index:10"></div>
    <nav class="mobile-more-panel">More destinations</nav>`);
  await page.locator('.mobile-more-backdrop').evaluate(el => el.addEventListener('click', () => {
    el.remove();
    document.querySelector('.mobile-more-panel')!.remove();
  }));

  expect(await dismissOverlay(page)).toBe(true);
  await expect(page.locator('.mobile-more-panel')).toHaveCount(0);
  expect(await dismissOverlay(page)).toBe(false);
});

test('Back returns control to Android when no overlay or visible modal remains', async ({ page }) => {
  await showOverlayHost(page, `
    <main>Current page</main>
    <div class="mud-overlay" style="display:none"></div>
    <div class="mud-dialog" style="display:none">Closed dialog</div>`);

  expect(await dismissOverlay(page)).toBe(false);
  await expect(page.locator('main')).toBeVisible();
});
