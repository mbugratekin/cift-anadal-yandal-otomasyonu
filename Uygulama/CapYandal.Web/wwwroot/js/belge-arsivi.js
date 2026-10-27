'use strict';
(() => {
    const archive = document.querySelector('.archive-workspace');
    if (!archive) return;
    archive.addEventListener('click', event => {
        const button = event.target.closest('button'); if(!button) return;
        const panel = button.closest('.evrak-panel'); if(!panel) return;
        if(button.hasAttribute('data-archive-upload')) {
            const editor=panel.querySelector('.archive-upload-editor'), form=editor?.querySelector('form'); if(!form) return;
            if(form.dataset.unknown === 'true' || form.dataset.saved === 'true') return;
            form.reset(); form.elements.SeriId.value=button.dataset.series || ''; form.elements.IstekId.value=button.dataset.request || '';
            form.elements.Tur.value=button.dataset.type || '';
            [...form.elements.Tur.options].forEach(o=>o.disabled=!!button.dataset.type && o.value!==button.dataset.type);
            const reason=form.querySelector('.archive-renewal-reason'); reason.hidden=button.dataset.reason!=='true';
            form.elements.Gerekce.required=!reason.hidden;
            form.querySelectorAll('[data-evrak-file-name],[data-evrak-file-message],[data-evrak-upload-message]').forEach(e=>e.textContent='');
            form.querySelector('[data-evrak-file-remove]').hidden=true;
            editor.hidden=false; form.querySelector('[data-evrak-file-input]').focus();
        }
        if(button.hasAttribute('data-archive-request')) { const editor=panel.querySelector('.archive-request-editor'); editor.hidden=!editor.hidden; }
        if(button.hasAttribute('data-archive-cancel')) { const editor=button.closest('.archive-editor,.document-review-row'); editor.hidden=true; }
        if(button.hasAttribute('data-archive-decision')) {
            const editor=document.getElementById(button.dataset.target), form=editor.querySelector('form');
            editor.hidden=false; form.elements.Karar.value=button.dataset.archiveDecision;
            form.elements.Gerekce.required=button.dataset.archiveDecision!=='2';
            if(button.dataset.archiveDecision==='2') form.requestSubmit(form.querySelector('button[type=submit]'));
            else form.elements.Gerekce.focus();
        }
    });
    archive.addEventListener('change', event=>{ if(event.target.matches('.belge-review-form select[name=Karar]')) event.target.form.elements.Gerekce.required=event.target.value!=='2'; });
    const buttons = [...archive.querySelectorAll('.archive-student')];
    const panels = [...archive.querySelectorAll('.archive-student-content')];
    function choose(button) {
        buttons.forEach(item => item.setAttribute('aria-expanded', String(item === button)));
        panels.forEach(panel => { panel.hidden = panel.id !== button?.getAttribute('aria-controls'); });
        if (!button) return;
        const programId = button.getAttribute('aria-controls')?.replace('arsiv-', '');
        if (programId) sessionStorage.setItem('capyandal-evrak-ogrenci', programId);
    }
    const remembered = sessionStorage.getItem('capyandal-evrak-ogrenci');
    const rememberedButton = remembered && buttons.find(button => button.getAttribute('aria-controls') === `arsiv-${remembered}`);
    if (rememberedButton) choose(rememberedButton);
    buttons.forEach(button => button.addEventListener('click', () => choose(button)));
    archive.querySelector('.archive-search')?.addEventListener('input', event => {
        const term = event.target.value.trim().toLocaleLowerCase('tr-TR');
        buttons.forEach(button => { button.closest('tr').hidden = !button.textContent.toLocaleLowerCase('tr-TR').includes(term); });
        const visible = buttons.filter(button => !button.closest('tr').hidden);
        if (!visible.some(button => button.getAttribute('aria-expanded') === 'true')) choose(visible[0]);
        archive.querySelector('.archive-search-message').textContent = visible.length ? '' : 'Öğrenci bulunamadı.';
    });
    archive.querySelectorAll('.course-period input,.course-period select').forEach(field => field.addEventListener('change', () => {
        if (field.form.reportValidity()) field.form.requestSubmit();
    }));
    archive.querySelectorAll('textarea').forEach(field => {
        const fit = () => { field.style.height = 'auto'; field.style.height = `${Math.max(36, field.scrollHeight)}px`; };
        field.addEventListener('input', fit);
    });
})();
