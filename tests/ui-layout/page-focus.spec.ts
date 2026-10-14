import { expect, test, type Page } from '@playwright/test';
import path from 'node:path';

declare global {
    interface Window {
        AuroraPageFocus: { register(page: HTMLElement): void; unregister(): void };
    }
}

async function settle(page: Page) {
    await page.evaluate(() => new Promise<void>(resolve =>
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
}

async function lock(page: Page) {
    await page.locator('#spell').focus();
    await page.evaluate(() => { document.querySelector<HTMLElement>('#page')!.inert = true; });
    await settle(page);
    await expect(page.locator('#spell')).not.toBeFocused();
}

async function unlock(page: Page) {
    await page.evaluate(() => { document.querySelector<HTMLElement>('#page')!.inert = false; });
    await settle(page);
}

test.beforeEach(async ({ page }) => {
    await page.setContent(`<!doctype html><html><body>
        <button id="outside">Navigation</button>
        <div id="page" data-focus-context="magic|character-a|0">
            <label><input id="spell" type="checkbox">Prepare Cure Wounds</label>
            <button id="next">Cure Wounds details</button>
        </div>
    </body></html>`);
    await page.addScriptTag({ path: path.resolve('Aurora.App/wwwroot/shortcuts.js') });
    await page.evaluate(() => window.AuroraPageFocus.register(document.querySelector<HTMLElement>('#page')!));
});

test('save keeps the page inert, then restores the same checkbox and tab order', async ({ page }) => {
    await page.evaluate(() => document.querySelector('#spell')!.addEventListener('change', () => {
        document.querySelector<HTMLElement>('#page')!.inert = true;
    }, { once: true }));
    await page.locator('#spell').focus();
    await page.keyboard.press('Space');
    await settle(page);
    await expect(page.locator('#spell')).toBeChecked();
    await expect(page.locator('#spell')).not.toBeFocused();
    await page.keyboard.press('Space');
    await expect(page.locator('#spell')).toBeChecked();
    await unlock(page);
    await expect(page.locator('#spell')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.locator('#next')).toBeFocused();
});

test('a second save before the restore frame keeps focus pending until its unlock', async ({ page }) => {
    await lock(page);
    await page.evaluate(async () => {
        const content = document.querySelector<HTMLElement>('#page')!;
        content.inert = false;
        // Deliver the unlock mutation before starting another save, but not its frame.
        await Promise.resolve();
        content.inert = true;
    });
    await settle(page);
    await expect(page.locator('#spell')).not.toBeFocused();
    await unlock(page);
    await expect(page.locator('#spell')).toBeFocused();
});

test('temporary aria-disabled preparation state does not prevent restoration', async ({ page }) => {
    await lock(page);
    await page.locator('#spell').evaluate(element => element.setAttribute('aria-disabled', 'true'));
    await unlock(page);
    await expect(page.locator('#spell')).toBeFocused();
});

test('a brief lock retains the target for the next ordinary save', async ({ page }) => {
    await page.locator('#spell').focus();
    await page.evaluate(async () => {
        const content = document.querySelector<HTMLElement>('#page')!;
        content.inert = true;
        await Promise.resolve();
        content.inert = false;
    });
    await settle(page);
    await expect(page.locator('#spell')).toBeFocused();
    await page.evaluate(() => { document.querySelector<HTMLElement>('#page')!.inert = true; });
    await settle(page);
    await expect(page.locator('#spell')).not.toBeFocused();
    await unlock(page);
    await expect(page.locator('#spell')).toBeFocused();
});

test('outside focus during the save is never stolen, even if it later returns to the body', async ({ page }) => {
    await lock(page);
    await page.locator('#outside').focus();
    await page.locator('#outside').evaluate(element => (element as HTMLElement).blur());
    await unlock(page);
    await expect(page.locator('#spell')).not.toBeFocused();
});

test('outside focus between unlock and restore is preserved', async ({ page }) => {
    await lock(page);
    await page.evaluate(() => {
        document.querySelector<HTMLElement>('#page')!.inert = false;
        document.querySelector<HTMLElement>('#outside')!.focus();
    });
    await settle(page);
    await expect(page.locator('#outside')).toBeFocused();
});

test('an outside pointer interaction cancels restoration even without a new focus target', async ({ page }) => {
    await lock(page);
    await page.mouse.click(600, 400);
    await unlock(page);
    await expect(page.locator('#spell')).not.toBeFocused();
});

for (const key of ['Tab', 'Escape']) {
    test(`${key} during the save cancels restoration`, async ({ page }) => {
        await lock(page);
        await page.keyboard.press(key);
        await unlock(page);
        await expect(page.locator('#spell')).not.toBeFocused();
    });
}

for (const context of ['session|character-a|0', 'magic|character-b|0', 'magic|character-a|1']) {
    test(`changed page context ${context} does not restore stale focus`, async ({ page }) => {
        await lock(page);
        await page.locator('#page').evaluate((element, value) => { (element as HTMLElement).dataset.focusContext = value; }, context);
        await unlock(page);
        await expect(page.locator('#spell')).not.toBeFocused();
    });
}

for (const change of ['replace', 'disable', 'hide'] as const) {
    test(`${change} of the original control prevents restoration`, async ({ page }) => {
        await lock(page);
        await page.locator('#spell').evaluate((element, action) => {
            if (action === 'replace') element.replaceWith(element.cloneNode(true));
            if (action === 'disable') (element as HTMLInputElement).disabled = true;
            if (action === 'hide') (element as HTMLElement).style.visibility = 'hidden';
        }, change);
        await unlock(page);
        await expect(page.locator('#spell')).not.toBeFocused();
    });
}

test('unregister cancels pending restoration', async ({ page }) => {
    await lock(page);
    await page.evaluate(() => window.AuroraPageFocus.unregister());
    await unlock(page);
    await expect(page.locator('#spell')).not.toBeFocused();
});

test('unregister after unlock cancels the scheduled restore frame', async ({ page }) => {
    await lock(page);
    await page.evaluate(async () => {
        document.querySelector<HTMLElement>('#page')!.inert = false;
        await Promise.resolve();
        window.AuroraPageFocus.unregister();
    });
    await settle(page);
    await expect(page.locator('#spell')).not.toBeFocused();
});
