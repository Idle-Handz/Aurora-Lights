import { expect, test } from '@playwright/test';
import path from 'node:path';

const legacyMonkFixture = path.resolve(
  process.cwd(),
  'Aurora.Tests/Fixtures/Characters/legacy-edited-arilith.dnd5e'
);

const routes = [
  { path: '/', heading: 'Characters' },
  { path: '/compendium', heading: 'Compendium' },
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
    await expect(page.getByRole('heading', { name: route.heading })).toBeVisible();
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
