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
