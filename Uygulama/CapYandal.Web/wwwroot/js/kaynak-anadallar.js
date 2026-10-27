(() => {
    "use strict";
    const section = document.querySelector('.source-departments');
    if (!section) return;
    const form = section.querySelector('#source-department-editor');
    const passive = section.querySelector('.source-show-passive');
    const tabs = [...section.querySelectorAll('.source-program-tabs button')];
    let program = '1';
    function refresh() {
        section.querySelectorAll('[data-source-panel]').forEach(panel => panel.hidden = panel.dataset.sourcePanel !== program);
        section.querySelectorAll('tr[data-source-program]').forEach(row => {
            row.hidden = row.dataset.sourceProgram !== program || (!passive.checked && row.dataset.sourceActive !== 'true');
        });
        tabs.forEach(button => button.setAttribute('aria-pressed', String(button.dataset.sourceProgram === program)));
        if (form) {
            form.elements.tur.value = program;
            form.querySelector('.source-selected-program').textContent = program === '1' ? 'ÇAP' : 'Yan Dal';
        }
    }
    passive.addEventListener('change', refresh);
    section.addEventListener('click', event => {
        const tab = event.target.closest('.source-program-tabs button');
        if (tab) {
            program = tab.dataset.sourceProgram;
            if (form) { form.reset(); form.hidden = true; }
            refresh(); return;
        }
        const button = event.target.closest('.source-add,.source-cancel');
        if (!button || !form) return;
        if (button.classList.contains('source-cancel')) { form.hidden = true; return; }
        form.reset(); refresh(); form.hidden = false; form.elements.ad.focus();
    });
    refresh();
})();
