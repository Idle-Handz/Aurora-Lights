import { expect, test, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const read = (file: string) => readFileSync(path.resolve(file), 'utf8');
const output = 'tests/ui-layout/FixtureRenderer/bin/Debug/net10.0';

async function showWorkspace(page: Page, component: 'shop' | 'equipment', options = { long: false, cloud: false }) {
  const componentName = component === 'shop' ? 'CharacterShopWorkspace' : 'CharacterEquipmentWorkspace';
  const markup = read(`${output}/${component === 'shop' ? `shop-${options.long ? 'long' : 'short'}` : 'equipment'}.html`);
  const css = read(`${output}/${componentName}.css`);

  // Use the App's actual Body wrapper, whose extra layout box caused the shop regression.
  // Keep only its conditional inert attribute under test control; fail clearly if the shell changes.
  const wrapper = read(`${output}/MainLayout.razor`)
    .match(/(<div\b[^>]*\binert="[^"]*"[^>]*>)\s*(?:<AppErrorBoundary\b[^>]*>)?@Body/)?.[1]
    .replace(/\binert="[^"]*"/, 'data-page-wrapper');
  if (!wrapper) throw new Error('MainLayout Body wrapper changed; update the App shell fixture.');

  await page.setContent(`<!doctype html><html><head>
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
    <style>${read(`${output}/MudBlazor.min.css`)}</style>
    <style>${read(`${output}/app.css`)}</style><style>${css}</style>
    <style>
      :root { --mud-appbar-height:64px }
      html,body { margin:0; padding:0; height:100%; font-family:'Segoe UI',sans-serif }
      #app { height:100% }
      @media(min-width:901px) { .app-main-content { margin-left:56px } }
    </style>
    </head><body><div id="app"><div class="mud-layout">
      <header class="mud-appbar mud-appbar-fixed-top"><div class="mud-toolbar mud-toolbar-appbar">Aurora: Reflections</div></header>
      <main class="mud-main-content app-main-content">
        <div class="char-tab-bar"><div class="char-tab">Layout fixture</div></div>
        <div class="mud-container mud-container-maxwidth-false app-content-container pa-4" style="--character-tabs-height:34px">
          ${options.cloud ? '<div class="mud-paper pa-3 mb-3"><div class="d-flex align-center flex-wrap gap-2"><span>Google Drive — Cloud save loaded</span><button class="mud-button-root">Save to Drive</button><button class="mud-button-root">Load Drive Save</button><button class="mud-button-root">Recovery Files</button></div></div>' : ''}
          ${wrapper}${markup}</div>
        </div>
      </main>
    </div><nav class="mobile-bottom-nav">${['Chars', 'Build', 'Gear', 'Magic', 'More'].map(name => `<a class="mobile-bottom-link"><span>${name}</span></a>`).join('')}</nav>
    <div class="app-status-bar">Ready</div></div></body></html>`);
}

async function expectNoHorizontalOverflow(page: Page) {
  expect(await page.evaluate(() => Math.max(document.body.scrollWidth, document.documentElement.scrollWidth) - innerWidth))
    .toBeLessThanOrEqual(1);
}

for (const scenario of [
  { width: 1440, height: 900, long: false, cloud: false },
  { width: 1280, height: 720, long: true, cloud: true }
]) {
  test(`desktop checkout stays visible while ${scenario.width}px shop results scroll`, async ({ page }) => {
    await page.setViewportSize(scenario);
    await showWorkspace(page, 'shop', scenario);
    expect(await page.locator('button.shop-row').count(), 'The fixture must contain a long results list').toBeGreaterThan(50);
    const checkout = page.locator('.shop-actions');
    await expect(checkout).toBeInViewport({ ratio: 1 });
    const before = await checkout.boundingBox();
    const listScroll = await page.locator('.shop-list-pane').evaluate(el => {
      el.scrollTop = el.scrollHeight;
      return el.scrollTop;
    });
    expect(listScroll, 'The results must have their own scroll area').toBeGreaterThan(1000);
    const after = await checkout.boundingBox();
    expect(Math.abs(after!.y - before!.y), 'Browsing results must not move checkout').toBeLessThan(1);
    expect(await page.locator('.app-content-container').evaluate(el => el.scrollHeight - el.clientHeight))
      .toBeLessThanOrEqual(1);
    if (scenario.long) {
      expect(await page.locator('.shop-detail-body').evaluate(el => el.scrollHeight - el.clientHeight),
        'A long description must scroll separately from checkout').toBeGreaterThan(100);
    }
    // Trial clicks check visibility and hit testing without moving a clipped button into place.
    await page.locator('.shop-actions .shop-button').first().click({ trial: true });
    await expectNoHorizontalOverflow(page);
  });
}

for (const viewport of [{ width: 360, height: 640 }, { width: 844, height: 390 }]) {
  test(`checkout remains reachable in a ${viewport.width}x${viewport.height} viewport`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await showWorkspace(page, 'shop', { long: true, cloud: true });
    // Short screens may scroll the detail pane or page, but must retain usable pane height.
    expect(await page.locator('.shop-body').evaluate(el => el.clientHeight)).toBeGreaterThan(100);
    const buy = page.locator('.shop-actions .shop-button').first();
    await buy.scrollIntoViewIfNeeded();
    await buy.click({ trial: true });
    await expect(buy).toBeInViewport({ ratio: 1 });
    await expectNoHorizontalOverflow(page);
  });
}

for (const width of [360, 900]) {
  test(`long inventory names do not overlap actions at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 844 });
    await showWorkspace(page, 'equipment');
    const rows = page.locator('.equipment-inventory-row:not(.equipment-inventory-header-row)');
    await expect(rows).toHaveCount(3);
    const measurements = await rows.evaluateAll(elements => elements.map(el => {
      const name = el.firstElementChild!;
      const actions = el.querySelector('.equipment-item-actions')!.getBoundingClientRect();
      const range = document.createRange();
      range.selectNodeContents(name);
      const text = range.getBoundingClientRect();
      return {
        name: name.textContent,
        overlap: Math.max(0, Math.min(text.right, actions.right) - Math.max(text.left, actions.left)) *
          Math.max(0, Math.min(text.bottom, actions.bottom) - Math.max(text.top, actions.top)),
        overflow: el.scrollWidth - el.clientWidth
      };
    }));
    for (const measurement of measurements) {
      expect(measurement.overlap, `${measurement.name}: text intersects the action buttons`).toBe(0);
      expect(measurement.overflow, `${measurement.name}: row overflows`).toBeLessThanOrEqual(1);
    }
    await expectNoHorizontalOverflow(page);
  });
}

test('the App interaction guard blocks both focus and clicks through the shop wrapper', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await showWorkspace(page, 'shop');
  const buy = page.locator('.shop-actions .shop-button').first();
  await buy.evaluate(el => el.addEventListener('click', () => el.setAttribute('data-clicked', 'true')));
  await buy.click();
  await expect(buy).toHaveAttribute('data-clicked', 'true');
  await buy.evaluate(el => { el.removeAttribute('data-clicked'); (el as HTMLElement).blur(); });
  await page.locator('[data-page-wrapper]').evaluate(el => { (el as HTMLElement).inert = true; });
  await buy.evaluate(el => (el as HTMLElement).focus());
  expect(await buy.evaluate(el => document.activeElement === el)).toBe(false);
  const bounds = (await buy.boundingBox())!;
  await page.mouse.click(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2);
  await expect(buy).not.toHaveAttribute('data-clicked');
  await page.locator('[data-page-wrapper]').evaluate(el => { (el as HTMLElement).inert = false; });
  await buy.click();
  await expect(buy).toHaveAttribute('data-clicked', 'true');
});
