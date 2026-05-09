window.scrollElementToTop = function (el) { if (el) el.scrollTop = 0; };

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
