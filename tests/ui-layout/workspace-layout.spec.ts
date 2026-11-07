import { expect, test, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const read = (file: string) => readFileSync(path.resolve(file), 'utf8');
const output = 'tests/ui-layout/FixtureRenderer/bin/Debug/net10.0';

async function showWorkspace(page: Page, component: 'shop' | 'equipment' | 'characters' | 'magic-all' | 'magic-prepared', options: { long?: boolean; cloud?: boolean; selected?: boolean; cloudExpanded?: boolean; weapons?: boolean; discovery?: boolean } = {}) {
  const componentNames = component === 'shop' ? ['CharacterShopWorkspace']
    : component === 'equipment' ? ['CharacterEquipmentWorkspace']
      : component.startsWith('magic-') ? ['CharacterMagicWorkspace', 'RichDescription']
        : ['CharacterBrowser', 'CharacterBrowserCard', 'CharacterLibraryShell'];
  const markup = read(`${output}/${component === 'shop' ? options.weapons ? `shop-weapons${options.discovery ? '-expanded' : ''}` : `shop-${options.selected === false ? 'browse' : options.long ? 'long' : 'short'}` : component}.html`);
  const css = componentNames.map(name => read(`${output}/${name}.css`)).join('\n');

  // Use the App's actual Body wrapper, whose extra layout box caused the shop regression.
  // Keep only its conditional inert attribute under test control; fail clearly if the shell changes.
  const wrapper = read(`${output}/MainLayout.razor`)
    .match(/(<div\b[^>]*\binert="[^"]*"[^>]*>)\s*(?:<AppErrorBoundary\b[^>]*>)?@Body/)?.[1]
    .replace(/\binert="[^"]*"/, 'data-page-wrapper');
  if (!wrapper) throw new Error('MainLayout Body wrapper changed; update the App shell fixture.');

  // Start expanded when requested: the preselected mobile checkout sheet should
  // block clicks on the underlying Drive controls, just as it does in the app.
  const cloudMarkup = options.cloud ? read(`${output}/cloud-status.html`) : '';

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
      <header class="mud-appbar mud-appbar-fixed-top"><div class="mud-toolbar mud-toolbar-appbar">Aurora Reflections</div></header>
      <main class="mud-main-content app-main-content">
        <div class="char-tab-bar"><div class="char-tab">Layout fixture</div></div>
        <div class="mud-container mud-container-maxwidth-false app-content-container pa-4" style="--character-tabs-height:34px">
          ${options.cloudExpanded ? cloudMarkup.replace('<details ', '<details open ') : cloudMarkup}
          ${wrapper}${component.startsWith('magic-') ? '<div class="page-save-bar">Auto-save off <button class="mud-button-root">Save</button></div>' : ''}${markup}</div>
        </div>
      </main>
    </div><nav class="mobile-bottom-nav">${['Chars', 'Build', 'Gear', 'Magic', 'More'].map(name => `<a class="mobile-bottom-link"><span>${name}</span></a>`).join('')}</nav>
    <div class="app-status-bar">Ready</div></div></body></html>`);
}

async function expectNoHorizontalOverflow(page: Page) {
  expect(await page.evaluate(() => Math.max(document.body.scrollWidth, document.documentElement.scrollWidth) - innerWidth))
    .toBeLessThanOrEqual(1);
}

for (const component of ['magic-all', 'magic-prepared'] as const) {
  for (const viewport of [{ width: 1280, height: 720 }, { width: 390, height: 844 }]) {
    test(`spell details scroll from attack damage to higher levels in ${component} at ${viewport.width}px`, async ({ page }) => {
      await page.setViewportSize(viewport);
      await showWorkspace(page, component, { long: true, cloud: true });
      const detail = page.getByRole('region', { name: 'Spell details' });
      const description = detail.locator('.magic-detail-description');
      const first = description.locator('p').first();
      const last = description.locator('p').last();
      expect(await description.evaluate(el => el.scrollHeight - el.clientHeight),
        'The description must retain its full height instead of becoming a tiny nested scroller').toBeLessThanOrEqual(1);
      if (viewport.width > 900) {
        // With the cloud/save bars on a short screen, the page can scroll to reveal the card.
        await detail.scrollIntoViewIfNeeded();
        await expect(detail).toBeInViewport();
        const listScrollBefore = await page.locator('.magic-list').evaluate(el => el.scrollTop);
        await detail.hover();
        await page.mouse.wheel(0, 300);
        await expect.poll(() => detail.evaluate(el => el.scrollTop)).toBeGreaterThan(0);
        await detail.evaluate(el => { el.scrollTop = 0; });
        await detail.focus();
        await page.keyboard.press('End');
        await expect(last).toBeInViewport({ ratio: 1 });
        expect(await page.locator('.magic-list').evaluate(el => el.scrollTop)).toBe(listScrollBefore);
      }
      await first.scrollIntoViewIfNeeded();
      await expect(first).toBeInViewport({ ratio: 1 });
      await last.scrollIntoViewIfNeeded();
      await expect(last).toBeInViewport({ ratio: 1 });
      await expectNoHorizontalOverflow(page);
      await page.screenshot({ path: `test-results/${component}-${viewport.width}.png`, fullPage: true });
    });
  }
}

for (const viewport of [{ width: 1280, height: 800 }, { width: 360, height: 800 }]) {
  test(`cloud character portrait and badge stay visible at ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await showWorkspace(page, 'characters');
    const card = page.locator('.character-browser-card').filter({ has: page.locator('[aria-label="Cloud save"]') });
    await expect(card).toHaveCount(1);
    await card.scrollIntoViewIfNeeded();
    await expect(card).toContainText('Cloud Portrait Test');
    await expect(card).toContainText('Wizard');
    await expect(card.locator('img')).toBeVisible();
    await expect.poll(() => card.locator('img').evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    const portrait = (await card.locator('.character-browser-portrait').boundingBox())!;
    const badge = (await card.locator('[aria-label="Cloud save"]').boundingBox())!;
    expect(badge.x).toBeGreaterThanOrEqual(portrait.x);
    expect(badge.y).toBeGreaterThanOrEqual(portrait.y);
    expect(badge.x + badge.width).toBeLessThanOrEqual(portrait.x + portrait.width / 2);
    expect(badge.y + badge.height).toBeLessThanOrEqual(portrait.y + portrait.height / 2);
    await card.click({ trial: true });
    await expectNoHorizontalOverflow(page);
    await page.screenshot({ path: `test-results/cloud-characters-${viewport.width}.png`, fullPage: true });
  });
}

test('character library header and actions fit a narrow viewport with 200% text', async ({ page }) => {
  await page.setViewportSize({ width: 288, height: 624 });
  await showWorkspace(page, 'characters');
  // Android WebView text zoom enlarges text while retaining CSS layout dimensions.
  await page.locator('.character-library-panel, .character-library-panel *').evaluateAll(elements => {
    const sizes = elements.map(el => parseFloat(getComputedStyle(el).fontSize));
    elements.forEach((el, i) => { (el as HTMLElement).style.fontSize = `${sizes[i] * 2}px`; });
  });
  const header = page.locator('.character-browser-header');
  const panelBounds = (await page.locator('.character-library-panel').boundingBox())!;
  const bounds = (await header.boundingBox())!;
  expect(bounds.x).toBeGreaterThanOrEqual(panelBounds.x);
  expect(bounds.x + bounds.width).toBeLessThanOrEqual(panelBounds.x + panelBounds.width);
  expect(await header.evaluate(el => el.scrollWidth - el.clientWidth), 'Large text must wrap inside the library header')
    .toBeLessThanOrEqual(1);
  expect(await page.locator('.character-library-panel').evaluate(el => el.scrollWidth - el.clientWidth),
    'Long character/group names and search controls must not expand the library grid')
    .toBeLessThanOrEqual(1);
  const actions = page.locator('.character-browser-create');
  await expect(actions).toHaveCount(3);
  for (const button of await actions.all()) {
    expect(await button.evaluate(el => el.scrollWidth - el.clientWidth), 'Every action label must fit its button')
      .toBeLessThanOrEqual(1);
    await button.scrollIntoViewIfNeeded();
    await button.click({ trial: true });
    await expect(button).toBeInViewport({ ratio: 1 });
  }
  await expectNoHorizontalOverflow(page);
});

for (const scenario of [
  { width: 1440, height: 900, long: false, cloud: false },
  { width: 1280, height: 720, long: true, cloud: true }
]) {
  test(`desktop checkout stays visible while ${scenario.width}px shop results scroll`, async ({ page }) => {
    await page.setViewportSize(scenario);
    // The compact default leaves checkout visible without scrolling the detail pane.
    await showWorkspace(page, 'shop', scenario);
    await expect(page.locator('.shop-discovery-toggle')).not.toBeVisible();
    await expect(page.locator('.shop-show-results')).not.toBeVisible();
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

for (const viewport of [{ width: 360, height: 640 }, { width: 384, height: 832 }]) {
  test(`mobile weapon browsing shows at least four readable rows at ${viewport.width}px`, async ({ browser }) => {
    const context = await browser.newContext({ viewport, isMobile: true, hasTouch: true });
    const page = await context.newPage();
    try {
      await showWorkspace(page, 'shop', { weapons: true, cloud: true });
      await expect(page.locator('.shop-discovery-toggle')).toHaveAttribute('aria-expanded', 'false');
      await expect(page.locator('.shop-discovery-summary')).toHaveText('Weapons');
      for (const selector of ['.shop-toolbar', '.shop-rail', '.shop-chip-row', '.shop-filters', '.shop-show-results'])
        await expect(page.locator(selector)).not.toBeVisible();
      const list = page.locator('.shop-list-pane');
      expect((await list.boundingBox())!.height).toBeGreaterThanOrEqual(384);
      await list.scrollIntoViewIfNeeded();
      const rows = page.locator('.shop-row');
      await rows.first().scrollIntoViewIfNeeded();
      const listBounds = (await list.boundingBox())!;
      for (const row of (await rows.all()).slice(0, 4)) {
        const bounds = (await row.boundingBox())!;
        expect(bounds.height, 'Item names and descriptions must retain readable row height').toBeGreaterThanOrEqual(60);
        expect(bounds.y + bounds.height, 'Four complete rows must fit inside the results pane').toBeLessThanOrEqual(listBounds.y + listBounds.height);
        await expect(row).toBeInViewport({ ratio: 1 });
      }
      await page.screenshot({ path: `test-results/shop-weapons-${viewport.width}.png` });
      await rows.last().scrollIntoViewIfNeeded();
      await rows.last().tap({ trial: true });
      await expectNoHorizontalOverflow(page);

      await showWorkspace(page, 'shop', { weapons: true, cloud: true, discovery: true });
      await expect(page.locator('.shop-discovery-toggle')).toHaveAttribute('aria-expanded', 'true');
      await expect(page.locator('.shop-search input')).toBeVisible();
      await expect(page.locator('.shop-rail-section')).toHaveCount(2);
      await expect(page.locator('.shop-chip-row')).toBeVisible();
      const show = page.getByRole('button', { name: 'Show 120 items', exact: true });
      await show.scrollIntoViewIfNeeded();
      await show.tap({ trial: true });
      await expectNoHorizontalOverflow(page);
    } finally {
      await context.close();
    }
  });
}

for (const viewport of [{ width: 360, height: 640 }, { width: 390, height: 844 }, { width: 844, height: 390 }]) {
  for (const cloud of [false, true]) {
    test(`shop items remain reachable for ${cloud ? 'cloud' : 'local'} saves at ${viewport.width}x${viewport.height}`, async ({ browser }) => {
      const context = await browser.newContext({ viewport, isMobile: true, hasTouch: true });
      const page = await context.newPage();
      try {
        await showWorkspace(page, 'shop', { cloud, selected: false, cloudExpanded: true });
        const list = page.locator('.shop-list-pane');
        expect(await page.locator('button.shop-row').count()).toBeGreaterThan(50);
        expect(await list.evaluate(el => el.clientHeight), 'The catalog needs usable height below the controls and Drive panel')
          .toBeGreaterThan(100);
        const first = page.locator('button.shop-row').first();
        await first.scrollIntoViewIfNeeded();
        await first.tap({ trial: true });
        await expect(first).toBeInViewport({ ratio: 1 });
        const last = page.locator('button.shop-row').last();
        await last.scrollIntoViewIfNeeded();
        await last.tap({ trial: true });
        await expect(last).toBeInViewport({ ratio: 1 });
        await expectNoHorizontalOverflow(page);
      } finally {
        await context.close();
      }
    });
  }
}

for (const viewport of [{ width: 360, height: 640 }, { width: 844, height: 390 }, { width: 1280, height: 720 }]) {
  test(`checkout remains reachable with Drive expanded at ${viewport.width}x${viewport.height}`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await showWorkspace(page, 'shop', { long: true, cloud: true, cloudExpanded: true });
    // Expanding the Drive controls may require scrolling the detail pane or page.
    // Checkout must remain reachable even when the controls leave less room.
    expect(await page.locator('.shop-body').evaluate(el => el.clientHeight)).toBeGreaterThan(100);
    const buy = page.locator('.shop-actions .shop-button').first();
    await buy.scrollIntoViewIfNeeded();
    await buy.click({ trial: true });
    await expect(buy).toBeInViewport({ ratio: 1 });
    await expectNoHorizontalOverflow(page);
  });
}

for (const width of [360, 1280]) {
  test(`Drive panel collapses without hiding its status at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 720 });
    await showWorkspace(page, 'shop', { cloud: true, selected: false });
    const panel = page.locator('.cloud-save-panel');
    const summary = panel.locator('summary');
    const save = panel.getByRole('button', { name: 'Save to Drive', exact: true });
    await expect(panel).not.toHaveAttribute('open');
    await expect(summary).toHaveText('Google Drive — Saved');
    await expect(save).not.toBeVisible();
    expect((await panel.boundingBox())!.height).toBeLessThan(60);
    await page.screenshot({ path: `test-results/cloud-panel-${width}-collapsed.png` });

    await summary.click();
    await expect(panel).toHaveAttribute('open');
    for (const action of ['Save to Drive', 'Load Drive Save', 'Disconnect character', 'Recovery Files'])
      await expect(panel.getByRole('button', { name: action, exact: true })).toBeVisible();
    await expect(panel.locator('.cloud-save-details')).toContainText('Saved to Google Drive.');
    await page.screenshot({ path: `test-results/cloud-panel-${width}-expanded.png` });

    await summary.focus();
    await page.keyboard.press('Space');
    await expect(panel).not.toHaveAttribute('open');
    await expect(save).not.toBeVisible();
    await expect(summary).toBeVisible();
    await page.keyboard.press('Enter');
    await expect(save).toBeVisible();
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
