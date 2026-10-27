"use strict";
document.querySelector('.approval-student-search input')?.addEventListener('input', event => {
    const term = event.target.value.trim().toLocaleLowerCase('tr-TR');
    const body = document.querySelector('.approval-roster tbody');
    const rows = [...body.querySelectorAll('tr:not(.approval-search-empty)')];
    rows.forEach(row => {
        row.hidden = !row.querySelector('th').textContent.toLocaleLowerCase('tr-TR').includes(term);
        if (row.hidden) row.querySelectorAll('.course-panel-toggle').forEach(button => {
            if (button.getAttribute('aria-expanded') === 'true') document.getElementById(button.getAttribute('aria-controls')).hidden = true;
            button.setAttribute('aria-expanded', 'false');
        });
    });
    let empty = body.querySelector('.approval-search-empty');
    if (!empty) { empty = document.createElement('tr'); empty.className = 'approval-search-empty'; const cell = document.createElement('td'); cell.colSpan = 3; cell.textContent = 'Aradığınız öğrenci kaydı bulunamadı'; empty.append(cell); body.append(empty); }
    empty.hidden = rows.some(row => !row.hidden);
    document.querySelector('.approval-search-message').textContent = '';
});
document.querySelectorAll('.course-panel-toggle').forEach(button => button.addEventListener('click', () => {
    const panel = document.getElementById(button.getAttribute('aria-controls'));
    if (!panel) return;
    const opening = panel.hidden;
    document.querySelectorAll('.course-panel-toggle').forEach(other => {
        other.setAttribute('aria-expanded', 'false');
        const otherPanel = document.getElementById(other.getAttribute('aria-controls'));
        if (otherPanel) otherPanel.hidden = true;
    });
    panel.hidden = !opening;
    document.querySelectorAll('.course-panel-toggle').forEach(other => {
        if (other.getAttribute('aria-controls') === panel.id) other.setAttribute('aria-expanded', String(opening));
    });
}));
document.querySelectorAll('.course-undo-direct').forEach(button => button.addEventListener('click', () => {
    const panel = document.getElementById(button.dataset.panelId);
    if (!panel) return;
    const toggle = [...document.querySelectorAll('.course-panel-toggle')].find(item => item.getAttribute('aria-controls') === panel.id);
    if (panel.hidden) toggle?.click();
    const decision = panel.querySelector('.course-approval select[name=Karar]');
    if (!decision) return;
    decision.value = '1';
    decision.dispatchEvent(new Event('change'));
    panel.querySelector('.course-shared-reason textarea')?.focus();
}));
document.querySelectorAll(".course-period input,.course-period select").forEach(field => field.addEventListener("change", () => {
    if (field.form.reportValidity()) field.form.requestSubmit();
}));
document.querySelectorAll(".course-list-block").forEach(block => {
    const form = block.querySelector(".course-command"), tbody = form?.querySelector(".course-selected"), dialog = block.querySelector("dialog");
    const approval = block.querySelector(".course-approval button"), original = tbody ? Array.from(tbody.children, row => row.cloneNode(true)) : [];
    const initialApprovalDisabled = approval?.disabled;
    const requestedCorrections = (form?.querySelector('.course-requested-corrections')?.value ?? '').split(',').filter(Boolean);
    const fixedCourses = requestedCorrections.length ? original.map(r => r.dataset.dersId).filter(id => !requestedCorrections.includes(id)) : [];
    const readonly = block.querySelector(".course-readonly"), approvalForm = block.querySelector(".course-approval");
    const decision = approvalForm?.elements.namedItem('Karar'), decisionReason = block.querySelector('.course-shared-reason');
    function decisionFields() {
        if (!decisionReason || !decision) return;
        const needed = decision.value !== '2';
        decisionReason.hidden = !needed;
        const field = decisionReason.querySelector('textarea');
        field.required = needed;
        if (!needed) field.value = '';
    }
    decision?.addEventListener('change', decisionFields);
    decisionFields();
    function editing(enabled) {
        if (!readonly || !form) return;
        readonly.hidden = enabled;
        form.hidden = !enabled;
        if (approvalForm) approvalForm.hidden = enabled;
        if (decisionReason && enabled) { decisionReason.hidden = false; decisionReason.querySelector("textarea").required = true; }
        else decisionFields();
    }
    let timer, busy = false, uncertain = false;
    const searchStatus = dialog?.querySelector(".course-search-status");
    const options = Array.from(dialog?.querySelectorAll("[data-option-id]") ?? []);
    dialog?.querySelector('.course-code-search input')?.addEventListener('input', event => {
        const term = event.target.value.trim().toLocaleLowerCase('tr-TR');
        options.forEach(row => { row.hidden = !`${row.querySelector('.course-code').textContent} ${row.cells[1].textContent}`.toLocaleLowerCase('tr-TR').includes(term); });
    });
    if (searchStatus) searchStatus.textContent = options.length ? "" : "Seçilebilecek ders bulunamadı.";
    function openPicker() { refresh(); dialog?.showModal(); }
    let backdropPress = false;
    const outsidePicker = event => {
        const bounds = dialog.getBoundingClientRect();
        return event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom;
    };
    dialog?.addEventListener('pointerdown', event => { backdropPress = event.target === dialog && outsidePicker(event); });
    dialog?.addEventListener('click', event => {
        if (!busy && backdropPress && event.target === dialog && outsidePicker(event)) dialog.close();
        backdropPress = false;
    });
    function message(text, persistent = false) { const node = block.querySelector(".course-message"); node.textContent = text; clearTimeout(timer); if (!persistent) timer = setTimeout(() => { node.textContent = ""; }, 4500); }
    function submissionStatus(text) {
        if (form?.dataset.studentSubmit !== "true") return;
        form.querySelectorAll('.course-row-status').forEach(cell => { cell.textContent = text; });
    }
    function selected() { return tbody ? Array.from(tbody.querySelectorAll("tr[data-ders-id]"), r => r.dataset.dersId) : []; }
    function refresh() {
        const ids = selected();
        if (tbody) {
            const format = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 2 });
            for (const [attribute, selector] of [["akts", ".course-total"], ["kredi", ".course-credit"]]) {
                const total = Array.from(tbody.querySelectorAll("tr[data-ders-id]"), row => Math.round(Number(row.dataset[attribute] ?? 0) * 100)).reduce((sum, value) => sum + value, 0) / 100;
                const output = block.querySelector(selector);
                if (output) output.textContent = format.format(total);
            }
        }
        options.forEach(row => {
            const chosen = ids.includes(row.dataset.optionId), button = row.querySelector("button");
            row.classList.toggle("course-chosen", chosen);
            const replacementLimit=requestedCorrections.length>0 && ids.filter(id=>!fixedCourses.includes(id)&&!requestedCorrections.includes(id)).length>=requestedCorrections.length;
            button.disabled = busy || chosen && fixedCourses.includes(row.dataset.optionId) || !chosen && (requestedCorrections.includes(row.dataset.optionId)||replacementLimit);
            button.textContent = chosen ? "−" : "+";
            button.setAttribute("aria-pressed", String(chosen));
            button.setAttribute("aria-label", `${row.cells[1].textContent.trim()} dersini ${chosen ? "çıkar" : "ekle"}`);
        });
        if (approval) approval.disabled = busy || initialApprovalDisabled || ids.slice().sort().join() !== original.map(r => r.dataset.dersId).sort().join();
    }
    block.addEventListener("click", event => {
        const button = event.target.closest("button"); if (!button || busy) return;
        if (button.classList.contains("course-open")) openPicker();
        if (button.classList.contains("course-close")) dialog?.close();
        if (button.classList.contains("course-add")) {
            const id = button.closest("tr").dataset.optionId;
            if (fixedCourses.includes(id) || !selected().includes(id) && requestedCorrections.includes(id)) return;
            if(requestedCorrections.length && !selected().includes(id) && selected().filter(value=>!fixedCourses.includes(value)&&!requestedCorrections.includes(value)).length>=requestedCorrections.length)return;
            if (selected().includes(id)) {
                Array.from(tbody.children).find(row => row.dataset.dersId === id)?.remove();
                refresh();
                return;
            }
            const source = Array.from(block.querySelectorAll("template[data-course-id]")).find(t => t.dataset.courseId === id);
            const row = source?.content.firstElementChild?.cloneNode(true);
            if (row && tbody) { tbody.append(row); refresh(); }
        }
        if (button.classList.contains("course-reset") && tbody) { tbody.replaceChildren(...original.map(r => r.cloneNode(true))); refresh(); editing(false); }
    });
    const panelId = block.closest('.course-student-details')?.id;
    document.querySelectorAll('.course-edit-direct').forEach(button => {
        if (button.dataset.panelId !== panelId) return;
        button.addEventListener('click', () => {
            if (busy || !form || !dialog) return;
            const panel = block.closest('.course-student-details');
            if (panel.hidden) document.querySelectorAll('.course-panel-toggle').forEach(toggle => {
                if (toggle.getAttribute('aria-controls') === panelId && toggle.getAttribute('aria-expanded') !== 'true') toggle.click();
            });
            editing(true); openPicker();
        });
    });
    block.querySelectorAll("form.course-command,form.course-approval").forEach(command => command.addEventListener("submit", async event => {
        event.preventDefault(); if (busy || uncertain) return;
        if (command === form && selected().length === 0 && form.dataset.bosListeIzinli !== "true") { message("En az bir ders seçin."); block.querySelector(".course-open")?.focus(); return; }
        if (!command.reportValidity()) return;
        const sharedReason = block.querySelector('.course-shared-reason textarea');
        const reasonRequired = command === form || decision?.value !== '2';
        if (sharedReason && reasonRequired && !sharedReason.value.trim()) { sharedReason.closest('label').hidden = false; sharedReason.setCustomValidity('Gerekçeyi yazın.'); sharedReason.reportValidity(); sharedReason.focus(); sharedReason.oninput = () => sharedReason.setCustomValidity(''); return; }
        const data = new FormData(command); if (sharedReason) data.set('Gerekce', sharedReason.value.trim()); busy = true;
        const buttons = Array.from(block.querySelectorAll("button")); const states = buttons.map(b => b.disabled); buttons.forEach(b => b.disabled = true);
        try {
            const response = await fetch(command.action, { method: "POST", body: data, credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (response.ok && response.redirected) { window.location.assign(response.url); return; }
            const text = await response.text();
            // 5xx/bağlantı hatasında sunucu işlemi tamamlamış olabilir; başarısız varsayıp tekrar göndermeyin.
            uncertain = response.status >= 500 || response.ok;
            submissionStatus(uncertain ? "Gönderim doğrulanamadı" : "Kaydedilemedi");
            const detail = [400, 409, 503].includes(response.status) ? text : "İşlem yapılamadı. Oturumu ve yetkinizi kontrol edin.";
            message(uncertain ? "Gönderim doğrulanamadı. Otomatik tekrar yapılmadı; sayfayı yenileyerek kontrol edin." : `Kaydedilemedi. ${detail}`, true);
        } catch { uncertain = true; submissionStatus("Gönderim doğrulanamadı"); message("Gönderim doğrulanamadı. Otomatik tekrar yapılmadı; önce sayfayı yenileyin.", true); }
        finally { busy = uncertain; buttons.forEach((b, i) => b.disabled = uncertain || states[i]); refresh(); }
    }));
    block.querySelectorAll('input[name=DuzeltilecekDersler]').forEach(check => check.addEventListener('change', () => {
        if (block.querySelector('input[name=DuzeltilecekDersler]:checked')) { if (decision) { decision.value = '3'; decisionFields(); } }
    }));
    refresh();
    block.querySelectorAll("form textarea").forEach(field => {
        const fit = () => { field.style.height = "auto"; field.style.height = `${field.scrollHeight}px`; };
        field.addEventListener("input", fit); fit();
    });
});
