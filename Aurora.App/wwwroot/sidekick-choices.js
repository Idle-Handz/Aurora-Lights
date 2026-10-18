window.sidekickChoices = {
    async focus(root, key) {
        if (!root) return false;
        // Wait until the dialog has rendered and its initial focus handling has finished.
        await new Promise(requestAnimationFrame);
        const target = Array.from(root.querySelectorAll('[data-sidekick-choice]'))
            .find(element => element.dataset.sidekickChoice === key);
        if (!target) return false;
        for (let parent = target.parentElement; parent && parent !== root; parent = parent.parentElement) {
            if (parent.tagName === 'DETAILS') parent.open = true;
        }
        const control = target.querySelector('select:not(:disabled), input:not([type="hidden"]):not(:disabled), button:not(:disabled), [tabindex="0"]');
        if (!control) return false;
        control.focus({ preventScroll: true });
        target.scrollIntoView({ block: 'center', inline: 'nearest' });
        return document.activeElement === control;
    }
};
