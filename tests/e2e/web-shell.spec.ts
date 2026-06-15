import { expect, test } from '@playwright/test';

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
