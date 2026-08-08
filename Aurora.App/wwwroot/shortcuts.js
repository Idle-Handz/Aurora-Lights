window.scrollElementToTop = function (el) { if (el) el.scrollTop = 0; };

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
