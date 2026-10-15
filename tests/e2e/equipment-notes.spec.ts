import { expect, test } from '@playwright/test';

test.afterEach(async ({ page }, testInfo) => {
  if (page.isClosed() || testInfo.status === 'skipped' || page.url() === 'about:blank') return;
  await page.locator('.web-nav-drawer a[href="/import"]').click();
  await page.getByRole('button', { name: 'Clear Session Imports', exact: true }).click();
  await expect(page.locator('.status-banner.success')).toContainText('Session workspace cleared.');
});

test('equipment notes retain the final draft across navigation, explicit save, and blur', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'chromium-desktop', 'The desktop drawer is exercised by this regression.');
  test.slow();

  // Make stale server renders arrive while later keystrokes are already in the DOM.
  let delayServerRenders = false;
  const pendingRenders = new Set<ReturnType<typeof setTimeout>>();
  await page.routeWebSocket(/\/_blazor\?/, socket => {
    const server = socket.connectToServer();
    server.onMessage(message => {
      if (!delayServerRenders) {
        socket.send(message);
        return;
      }
      const timer = setTimeout(() => {
        pendingRenders.delete(timer);
        if (!page.isClosed()) socket.send(message);
      }, 75);
      pendingRenders.add(timer);
    });
  });

  await page.goto('/', { waitUntil: 'networkidle' });
  await page.getByLabel('Character Name', { exact: true }).fill('Equipment Notes Regression');
  await page.getByRole('button', { name: 'Create New Character', exact: true }).click();
  await expect(page).toHaveURL(/\/build$/, { timeout: 60_000 });
  await page.locator('.web-nav-drawer a[href="/equipment"]').click();

  const notes = page.locator('.equipment-notes-area').first();
  const saveNotes = page.getByRole('button', { name: 'Save Notes', exact: true });
  const finalDraft = 'A complete note typed without interrupted focus.';
  await expect(notes).toBeVisible();
  delayServerRenders = true;
  await notes.fill('');
  await notes.pressSequentially(finalDraft, { delay: 5 });
  await expect(notes).toHaveValue(finalDraft);
  await expect(notes).toBeEnabled();
  await expect(saveNotes).toBeVisible();
  await expect.poll(() => pendingRenders.size).toBe(0);
  delayServerRenders = false;

  await page.locator('.web-nav-drawer a[href="/workspace"]').click();
  await expect(page).toHaveURL(/\/workspace$/);
  await page.locator('.web-nav-drawer a[href="/equipment"]').click();
  await expect(notes).toHaveValue(finalDraft);

  await notes.fill('Saved explicitly while the draft is focused.');
  await saveNotes.click();
  await expect(saveNotes).toBeHidden();
  await page.locator('.web-nav-drawer a[href="/workspace"]').click();
  await page.locator('.web-nav-drawer a[href="/equipment"]').click();
  await expect(notes).toHaveValue('Saved explicitly while the draft is focused.');

  await notes.fill('Final text committed on blur.');
  await notes.press('Tab');
  await expect(saveNotes).toBeHidden();
  await page.locator('.web-nav-drawer a[href="/workspace"]').click();
  await page.locator('.web-nav-drawer a[href="/equipment"]').click();
  await expect(notes).toHaveValue('Final text committed on blur.');
});
