import { expect, test } from '@playwright/test';
import path from 'node:path';

const legacyMonkFixture = path.resolve(
  process.cwd(),
  'Aurora.Tests/Fixtures/Characters/legacy-edited-arilith.dnd5e'
);

const routes = [
  { path: '/', heading: 'Characters' },
  { path: '/overview', heading: 'Web workspace' },
  { path: '/compendium', heading: 'Compendium' },
  { path: '/workspace', heading: 'Workspace' },
  { path: '/character', heading: 'Character Overview' },
  { path: '/equipment', heading: 'Equipment' },
  { path: '/magic', heading: 'Magic' }
];

for (const route of routes) {
  test(`${route.heading} renders without obvious layout regressions`, async ({ page }, testInfo) => {
    const consoleProblems: string[] = [];

    page.on('console', message => {
      if (message.type() === 'error') {
        consoleProblems.push(message.text());
      }
    });

    const response = await page.goto(route.path, { waitUntil: 'domcontentloaded' });

    expect(response?.ok(), `${route.path} should load successfully`).toBeTruthy();
    await expect(page.getByRole('heading', { name: route.heading, exact: true })).toBeVisible();
    await expect(page.locator('#blazor-error-ui')).toBeHidden();

    const overflow = await page.evaluate(() => {
      const html = document.documentElement;
      const body = document.body;

      return {
        bodyClientWidth: body.clientWidth,
        bodyScrollWidth: body.scrollWidth,
        htmlClientWidth: html.clientWidth,
        htmlScrollWidth: html.scrollWidth
      };
    });

    expect(
      Math.max(overflow.bodyScrollWidth, overflow.htmlScrollWidth),
      `${route.path} should not create horizontal page overflow`
    ).toBeLessThanOrEqual(Math.max(overflow.bodyClientWidth, overflow.htmlClientWidth) + 2);

    await testInfo.attach(`${route.heading.toLowerCase()}-${testInfo.project.name}.png`, {
      body: await page.screenshot({ fullPage: false }),
      contentType: 'image/png'
    });

    expect(consoleProblems).toEqual([]);
  });
}

test('Topbar search preserves imported content and the edited active character', async ({ page }, testInfo) => {
  test.skip(
    testInfo.project.name === 'chromium-mobile',
    'Session continuity is covered once on desktop; mobile navigation has separate layout coverage.'
  );
  test.slow();

  const markerName = 'Search Marker & + # ?';
  const markerId = 'ID_TEST_TOPBAR_SESSION_MARKER';
  await page.goto('/import', { waitUntil: 'networkidle' });
  await page.locator('#phase0-upload').setInputFiles({
    name: 'topbar-session-marker.xml',
    mimeType: 'text/xml',
    buffer: Buffer.from(`<elements>
      <element name="Search Marker &amp; + # ?" type="Feat" source="Topbar Regression" id="${markerId}">
        <description><p>Imported content must survive topbar search.</p></description>
      </element>
    </elements>`)
  });
  await expect(page.locator('.status-banner.success')).toContainText('Imported 1 file');

  await page.locator('.web-nav-drawer a[href="/"]').click();
  await page.getByLabel('Character Name', { exact: true }).fill('Search Session Character');
  await page.getByRole('button', { name: 'Create New Character', exact: true }).click();
  await expect(page).toHaveURL(/\/build$/, { timeout: 60_000 });

  await page.locator('.web-nav-drawer a[href="/manage"]').click();
  await page.locator('.character-info-editor').getByLabel('Character', { exact: true })
    .fill('Edited Search Session Character');
  await page.locator('.character-info-editor').getByLabel('Player', { exact: true })
    .fill('Preserved Player');
  await page.getByRole('button', { name: 'Save Session Changes', exact: true }).click();
  await expect(page.locator('.character-library-status.success'))
    .toHaveText('Character details saved in the current web session.');

  await page.locator('.web-nav-drawer a[href="/workspace"]').click();
  const sourceFile = page.locator('.detail-item').filter({
    has: page.locator('.detail-label', { hasText: /^Source File$/ })
  }).locator('strong');
  await expect(sourceFile).toBeVisible();
  const originalFileName = await sourceFile.innerText();

  const topbarSearch = page.getByPlaceholder('Compendium search', { exact: true });
  await topbarSearch.fill(markerName);
  await topbarSearch.press('Enter');
  await expect(page).toHaveURL(url =>
    url.pathname === '/compendium' && url.searchParams.get('q') === markerName);
  await expect(page.getByRole('cell', { name: markerName, exact: true })).toBeVisible();
  await expect(page.locator('.origin-pill.session')).toHaveCount(1);

  // The search button must use the same in-circuit navigation as Enter.
  await topbarSearch.fill(markerId);
  await page.getByRole('button', { name: 'Search compendium', exact: true }).click();
  await expect(page).toHaveURL(url =>
    url.pathname === '/compendium' && url.searchParams.get('q') === markerId);
  await expect(page.getByRole('cell', { name: markerName, exact: true })).toBeVisible();

  await page.locator('.web-nav-drawer a[href="/workspace"]').click();
  await expect(page.locator('.detail-grid')).toContainText('Edited Search Session Character');
  await expect(page.locator('.detail-grid')).toContainText('Preserved Player');
  await expect(sourceFile).toHaveText(originalFileName);
  const xmlCount = page.locator('.summary-item').filter({
    has: page.locator('.summary-caption', { hasText: /^XML files$/ })
  }).locator('.summary-number');
  await expect(xmlCount).toHaveText('1');
  await expect(page.locator('#blazor-error-ui')).toBeHidden();
});

test('Build picker distinguishes the current choice from unavailable owned choices', async ({ page }, testInfo) => {
  test.skip(
    testInfo.project.name === 'chromium-mobile',
    'Picker behavior is covered once on desktop; mobile navigation has separate layout coverage.'
  );
  test.slow();

  await page.goto('/import', { waitUntil: 'networkidle' });
  await page.locator('#phase0-upload').setInputFiles(legacyMonkFixture);
  await expect(page.locator('.status-banner.success')).toContainText('Imported 1 file', {
    timeout: 15_000
  });

  await page.locator('.web-nav-drawer a[href="/"]').click();
  await expect(page.getByRole('heading', { name: 'Characters' })).toBeVisible();
  await expect(page.locator('.character-browser-name')).toHaveText('Fixture Legacy Edited Arilith');

  await page.getByRole('button', { name: 'Use in Workspace' }).click();
  await expect(page).toHaveURL(/\/workspace$/, { timeout: 60_000 });

  await page.locator('.web-nav-drawer a[href="/character"]').click();
  await expect(page.getByRole('heading', { name: 'Character Overview' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Fixture Legacy Edited Arilith' })).toBeVisible();

  await page.locator('.web-nav-drawer a[href="/build"]').click();
  await expect(page.getByRole('heading', { name: 'Build Character' })).toBeVisible();

  const firstSkillSlot = page.locator('.build-web-rule').filter({
    has: page.locator('.build-web-rule-label', {
      hasText: /^Skill Proficiency \(Monk\) \(1\)$/
    })
  });
  await expect(firstSkillSlot).toHaveCount(1);
  await expect(firstSkillSlot.locator('.build-web-rule-choice')).toHaveText('Acrobatics');
  await firstSkillSlot.getByRole('button', { name: 'Change' }).click();

  const currentChoice = page.locator('.picker-result-card').filter({
    has: page.locator('.picker-result-title', { hasText: /^Acrobatics$/ })
  });
  const ownedElsewhere = page.locator('.picker-result-card').filter({
    has: page.locator('.picker-result-title', { hasText: /^Stealth$/ })
  });

  await expect(currentChoice.getByRole('button')).toHaveText('Selected');
  await expect(currentChoice.getByRole('button')).toBeEnabled();
  await expect(ownedElsewhere.getByRole('button')).toHaveText('Unavailable');
  await expect(ownedElsewhere.getByRole('button')).toBeDisabled();
});
