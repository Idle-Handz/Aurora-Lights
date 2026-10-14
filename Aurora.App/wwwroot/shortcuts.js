window.scrollElementToTop = function (el) { if (el) el.scrollTop = 0; };

window.AuroraBack = {
    dismissOverlay: function () {
        // Hit-test the actual stack so a menu opened above a dialog closes first.
        // Use the existing backdrop handler: dialogs that forbid dismissal keep
        // their policy and consume Back instead of losing the underlying page.
        const overlay = document.elementsFromPoint(innerWidth / 2, innerHeight / 2)
            .map(el => el.closest('.mud-overlay, .mobile-more-backdrop'))
            .find(el => el !== null);
        if (overlay) {
            overlay.click();
            return true;
        }
        return Array.from(document.querySelectorAll('.mud-dialog'))
            .some(el => el.getClientRects().length > 0);
    }
};

window.AuroraTheme = {
    apply: function (themeId, isDark) {
        const root = document.documentElement;
        root.dataset.auroraTheme = themeId;
        root.style.colorScheme = isDark ? 'dark' : 'light';

        try {
            window.localStorage.setItem('aurora.theme', themeId);
        } catch {
            // Local storage can be unavailable in restricted WebView contexts.
        }
    }
};

window.AuroraShortcuts = {
    register: function (dotnetRef) {
        window._auroraShortcutRef = dotnetRef;
        document.addEventListener('keydown', function (e) {
            if (e.ctrlKey && e.key === 's') {
                e.preventDefault();
                if (window._auroraShortcutRef) {
                    window._auroraShortcutRef.invokeMethodAsync('OnCtrlS');
                }
            }
        });
    },
    unregister: function () {
        window._auroraShortcutRef = null;
    }
};

// The save lock remains inert. Return keyboard focus only when that lock displaced
// it, without following a stale element into a new character/page or stealing focus.
window.AuroraPageFocus = (() => {
    let cleanup;
    return {
        register: function (page) {
            cleanup?.();
            if (!(page instanceof HTMLElement)) return;
            let remembered = null;
            let candidate = null;
            let locked = page.inert;
            let frame = 0;
            let context = page.dataset.focusContext;
            const cancel = () => {
                remembered = candidate = null;
                if (frame) cancelAnimationFrame(frame);
                frame = 0;
            };
            const remember = element => {
                remembered = { element, context: page.dataset.focusContext, url: location.href };
            };
            const beginLock = () => {
                if (locked) return;
                locked = true;
                // A second save can begin before the unlock frame restores focus.
                // Keep the same candidate until a real unlock or user cancellation.
                if (frame) cancelAnimationFrame(frame);
                frame = 0;
                candidate = remembered ?? candidate;
            };
            const focusIn = event => {
                if (page.contains(event.target) && !page.inert) {
                    cancel();
                    remember(event.target);
                } else if (event.target !== document.body && event.target !== document.documentElement) {
                    cancel();
                }
            };
            const focusOut = event => {
                if (event.target !== remembered?.element) return;
                // Chromium may blur as soon as inert is applied, before the observer runs.
                if (page.inert) beginLock();
                else remembered = null;
            };
            const pointerDown = event => {
                if (!page.contains(event.target)) cancel();
            };
            const keyDown = event => {
                if ((locked || frame) && (event.key === 'Tab' || event.key === 'Escape')) cancel();
            };
            const observer = new MutationObserver(() => {
                if (context !== page.dataset.focusContext) {
                    cancel();
                    context = page.dataset.focusContext;
                }
                if (page.inert) { beginLock(); return; }
                if (!locked) return;
                locked = false;
                const restore = candidate;
                remembered = null;
                if (!restore) return;
                frame = requestAnimationFrame(() => {
                    frame = 0;
                    if (page.inert) return;
                    candidate = null;
                    const element = restore.element;
                    const active = document.activeElement;
                    if (!page.isConnected || page.inert || !element.isConnected || !page.contains(element)
                        || restore.context !== page.dataset.focusContext || restore.url !== location.href
                        || (active !== document.body && active !== document.documentElement && active !== element)
                        || element.matches(':disabled') || element.closest('[inert]')
                        || element.getClientRects().length === 0 || getComputedStyle(element).visibility === 'hidden') return;
                    element.focus({ preventScroll: true });
                    // A short lock may never blur; focus() then emits no focusin.
                    if (document.activeElement === element) remember(element);
                });
            });
            observer.observe(page, { attributes: true, attributeFilter: ['inert', 'data-focus-context'] });
            document.addEventListener('focusin', focusIn, true);
            document.addEventListener('focusout', focusOut, true);
            document.addEventListener('pointerdown', pointerDown, true);
            document.addEventListener('keydown', keyDown, true);
            window.addEventListener('blur', cancel);
            window.addEventListener('popstate', cancel);
            window.addEventListener('hashchange', cancel);
            if (!page.inert && page.contains(document.activeElement)) remember(document.activeElement);
            cleanup = () => {
                cancel();
                observer.disconnect();
                document.removeEventListener('focusin', focusIn, true);
                document.removeEventListener('focusout', focusOut, true);
                document.removeEventListener('pointerdown', pointerDown, true);
                document.removeEventListener('keydown', keyDown, true);
                window.removeEventListener('blur', cancel);
                window.removeEventListener('popstate', cancel);
                window.removeEventListener('hashchange', cancel);
            };
        },
        unregister: function () { cleanup?.(); cleanup = null; }
    };
})();
