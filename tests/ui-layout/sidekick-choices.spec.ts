import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
const output = 'tests/ui-layout/FixtureRenderer/bin/Debug/net10.0';
const read = (file: string) => readFileSync(path.resolve(output, file), 'utf8');
for (const width of [1100, 390]) {
  test('sidekick choices are readable and reachable at ' + width + 'px', async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.setContent('<!doctype html><html><head><meta name="viewport" content="width=device-width, initial-scale=1">'
      + '<style>' + read('SidekickChoicesPanel.css')
      + 'body { padding:16px; font-family:Segoe UI,sans-serif; background:#1e1e2e; color:#cdd6f4;'
      + '--mud-palette-divider:#45475a; --mud-palette-surface:#313244; --mud-palette-text-secondary:#a6adc8;'
      + '--mud-palette-primary:#89b4fa; --mud-palette-error:#f38ba8; }</style></head><body>'
      + read('sidekick-choices.html') + '</body></html>');
    await page.addScriptTag({ content: readFileSync(path.resolve('Aurora.Reflections/wwwroot/sidekick-choices.js'), 'utf8') });
    const choices = page.locator('.sidekick-choices');
    // The live Blazor callback calls the same helper when the dialog opens or a link is clicked.
    await page.evaluate(() => {
      const root = document.querySelector('.sidekick-choices')!;
      root.querySelectorAll<HTMLButtonElement>('[data-sidekick-jump]').forEach(button => {
        button.addEventListener('click', () => (window as any).sidekickChoices.focus(root, button.dataset.sidekickJump));
      });
    });
    await page.locator('[data-sidekick-jump="asi:4:0"]').click();
    await expect(page.getByRole('combobox', { name: 'Level 4: +1 ability (1/2)', exact: true })).toBeFocused();
    await page.locator('[data-sidekick-jump="spell:0"]').click();
    await expect(page.getByRole('button', { name: 'Choose known spell 1', exact: true })).toBeFocused();
    await page.locator('summary').click();
    await page.getByLabel('Base Dexterity', { exact: true }).fill('16');
    await page.getByRole('combobox', { name: 'Level 4: +1 ability (1/2)', exact: true }).selectOption('Wisdom');
    await page.getByRole('combobox', { name: 'Skill proficiency 1', exact: true }).selectOption('History');
    await page.getByRole('combobox', { name: 'HP for new levels', exact: true }).selectOption('Roll');
    await page.getByRole('combobox', { name: 'Level 2 HP method', exact: true }).selectOption('Roll');
    await page.getByRole('spinbutton', { name: 'Level 3 roll (d8)', exact: true }).fill('7');
    await expect(page.getByRole('spinbutton', { name: 'Level 3 roll (d8)', exact: true })).toHaveValue('7');
    for (const name of ['Choose cantrip 1', 'Clear cantrip 1', 'Choose known spell 1']) {
      const button = page.getByRole('button', { name, exact: true });
      await button.scrollIntoViewIfNeeded();
      await button.click({ trial: true });
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
    await page.screenshot({ path: 'test-results/sidekick-choices-' + width + '.png', fullPage: true });
    // Check scrolling within a dialog-sized container, and scope duplicate keys to that dialog.
    await choices.evaluate(root => {
      const decoy = root.cloneNode(true) as HTMLElement;
      decoy.id = 'other-sidekick'; root.before(decoy);
      (root as HTMLElement).id = 'active-sidekick';
      (root as HTMLElement).style.cssText = 'max-height:350px;overflow:auto';
    });
    await page.evaluate(() => (window as any).sidekickChoices.focus(document.querySelector('#active-sidekick'), 'spell:0'));
    await expect(page.locator('#active-sidekick').getByRole('button', { name: 'Choose known spell 1', exact: true })).toBeFocused();
    expect(await page.locator('#active-sidekick').evaluate(root => root.scrollTop)).toBeGreaterThan(0);
    expect(await page.evaluate(() => (window as any).sidekickChoices.focus(document.querySelector('#active-sidekick'), 'not-a-choice'))).toBe(false);
  });
}
