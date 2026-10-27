"use strict";
// Not işlemi — aynı form/sürüm gönderilir; belirsiz sonuçta otomatik tekrar yapılmaz.
document.querySelectorAll(".ders-deneme-form").forEach(form => {
    let busy = false;
    const state = form.elements.namedItem('Durum'), passed = form.elements.namedItem('GecilenDonemId');
    const row = form.closest('tr'), note = form.elements.namedItem('Not'), reason = form.elements.namedItem('Gerekce');
    const numeric = form.elements.namedItem('SayisalNot');
    const original = { note: note?.value, numeric: numeric?.value, state: state?.value, stateHidden: state?.hidden, passed: passed?.value, passedDisabled: passed?.disabled };
    note?.closest('td')?.addEventListener('click', event => { if (event.target === note.closest('td') && !note.disabled) note.focus(); });
    const fit = field => { field.style.height = "auto"; field.style.height = `${Math.max(36, field.scrollHeight)}px`; };
    form.querySelector('.grade-edit')?.addEventListener('click', event => {
        event.currentTarget.hidden = true;
        note.disabled = false; if(numeric) numeric.readOnly = false;
        state.hidden = false;
        row.querySelector('.grade-state-text').hidden = true;
        form.querySelector('.grade-save').hidden = false;
        form.querySelector('.grade-cancel').hidden = false;
        if (reason) { reason.hidden = false; reason.required = true; fit(reason); }
        syncState();
        note.focus();
    });
    form.querySelector('.grade-cancel')?.addEventListener('click', () => {
        if (busy) return;
        note.value = original.note; note.disabled = true; if(numeric) { numeric.value = original.numeric; numeric.readOnly = true; } state.value = original.state; state.hidden = original.stateHidden;
        if (passed) { passed.value = original.passed; passed.disabled = original.passedDisabled; }
        row.querySelector('.grade-state-text').hidden = !original.stateHidden;
        form.querySelector('.grade-edit').hidden = false;
        form.querySelector('.grade-save').hidden = true; form.querySelector('.grade-cancel').hidden = true;
        if (reason) { reason.value = ''; reason.hidden = true; reason.required = false; }
        form.querySelector('.ders-deneme-mesaji').textContent = '';
    });
    const syncState = () => {
        const exempt = state?.value === "5";
        if (passed) {
            passed.disabled = state.value !== "3";
            if (passed.disabled) passed.value = "";
            else if (!passed.value) passed.value = original.passed || form.elements.namedItem('DonemId').value;
        }
        if (exempt && note && !note.value) note.value = "M";
    };
    state?.addEventListener("change", syncState);
    note?.addEventListener("change", () => {
        if (note.value === "M" && state?.tagName === "SELECT") { state.value = "5"; syncState(); }
    });
    form.querySelectorAll("textarea").forEach(field => {
        field.addEventListener("input", () => fit(field)); fit(field);
    });
    form.addEventListener("submit", async event => {
        event.preventDefault(); if (busy || !form.reportValidity()) return;
        const decision = event.submitter?.dataset.gradeDecision;
        if (decision === '3' && reason && !reason.value.trim()) { reason.hidden = false; reason.required = true; fit(reason); reason.focus(); return; }
        const data = new FormData(form), button = event.submitter ?? form.querySelector('button[type="submit"]'), message = form.querySelector(".ders-deneme-mesaji");
        if(note) data.set('Not',note.value);
        if (decision) data.set('OnayDurumu', decision);
        busy = true; button.disabled = true; message.textContent = "";
        try {
            const response = await fetch(form.action, { method: "POST", body: data, credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (response.ok && !response.redirected) { window.location.reload(); return; }
            message.textContent = response.status === 400 || response.status === 409 || response.status === 503
                ? await response.text() : "Oturum, yetki veya ders bilgisi doğrulanamadı.";
            message.tabIndex = -1;
            message.scrollIntoView({ block: "nearest" });
            message.focus({ preventScroll: true });
        } catch { message.textContent = "İşlem sonucu doğrulanamadı. Otomatik tekrar yapılmadı; önce sayfayı yenileyin."; }
        finally { busy = false; button.disabled = false; }
    });
});
