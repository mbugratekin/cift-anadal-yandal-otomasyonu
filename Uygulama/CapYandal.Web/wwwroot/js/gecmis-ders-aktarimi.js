"use strict";
// Toplu aktarım — satırlar aynı dönem için tek komuttur; hata veya belirsizlikte kendiliğinden gönderilmez.
document.querySelectorAll(".gecmis-aktarim").forEach(section => {
    const form = section.querySelector("form"), period = form.elements.DonemId;
    const rows = form.querySelector("tbody"), add = form.querySelector(".gecmis-satir-ekle");
    const send = form.querySelector('[type="submit"]'), message = form.querySelector('[role="alert"]');
    let options = null, loading = false, busy = false;
    const fill = (select, values, label) => {
        select.replaceChildren(new Option(label, ""));
        values.forEach(item => select.add(new Option(item.ad, item.id)));
    };
    const reindex = () => rows.querySelectorAll("tr").forEach((row, index) => {
        row.querySelectorAll("[data-field]").forEach(field => { field.name = `Satirlar[${index}].${field.dataset.field}`; });
    });
    const createRow = () => {
        if (!options || busy || rows.children.length >= 100) return;
        const row = section.querySelector("template").content.firstElementChild.cloneNode(true);
        fill(row.querySelector('[data-field="DersId"]'), options.dersler.map(x => ({ id: x.id, ad: `${x.kod} — ${x.ad}` })), "Ders seçin");
        const state = row.querySelector('[data-field="Durum"]'), passed = row.querySelector('[data-field="GecilenDonemId"]');
        const note = row.querySelector('[data-field="Not"]'), numeric = row.querySelector('[data-field="SayisalNot"]');
        fill(passed, options.donemler, "Geçilen dönem seçin");
        state.addEventListener("change", () => {
            const exempt = state.value === "5";
            passed.disabled = state.value !== "3"; passed.required = !passed.disabled; if (passed.disabled) passed.value = "";
            if (exempt && !note.value) note.value = "M";
        });
        note.addEventListener("change", () => {
            if (note.value === "M") { state.value = "5"; state.dispatchEvent(new Event("change")); }
        });
        row.querySelector("button").addEventListener("click", () => { if (!busy) { row.remove(); reindex(); } });
        rows.append(row); reindex();
    };
    period.addEventListener("change", () => {
        form.elements.DonemSurum.value = options?.donemler.find(x => x.id === period.value)?.surum ?? "";
    });
    async function loadOptions() {
        if (options || loading) return;
        loading = true;
        try {
            const preparation = new FormData(); preparation.set('programId', form.elements.ProgramId.value);
            preparation.set('__RequestVerificationToken', form.querySelector('[name=__RequestVerificationToken]').value);
            const prepared = await fetch(section.dataset.prepareUrl, { method: 'POST', credentials: 'same-origin', body: preparation });
            if (!prepared.ok || prepared.redirected) { message.textContent = [400,409,503].includes(prepared.status) ? await prepared.text() : 'Dönem hazırlama yetkisi doğrulanamadı.'; return; }
            const response = await fetch(section.dataset.optionsUrl, { credentials: "same-origin" });
            if (!response.ok || response.redirected) throw new Error();
            options = await response.json();
            form.elements.ProgramSurum.value = options.programSurum; form.elements.SablonSurum.value = options.sablonSurum;
            fill(period, options.donemler, "Eklemek istediğiniz geçmiş dönemi seçin");
            const available = options.donemler.length > 0 && options.dersler.length > 0;
            form.classList.toggle("import-unavailable", !available);
            period.disabled = !available; add.disabled = !available; send.disabled = !available;
            if (available) createRow(); else message.textContent = "Aktarılabilecek geçmiş bir dönem bulunmamaktadır (Tüm dönemler aktarılmış veya yeni kayıtlı öğrenci).";
        } catch { message.textContent = "Yetki, müfredat veya dönem seçenekleri doğrulanamadı. Sayfayı yenileyin."; }
        finally { loading = false; }
    }
    const opener = section.querySelector('.history-import-open'), panel = section.querySelector('.history-import-body');
    opener.addEventListener('click', () => { panel.hidden = false; opener.setAttribute('aria-expanded', 'true'); void loadOptions(); });
    section.querySelector('.history-import-close').addEventListener('click', () => { panel.hidden = true; opener.setAttribute('aria-expanded', 'false'); opener.focus(); });
    add.addEventListener("click", createRow);
    const reason = form.elements.Gerekce;
    reason.addEventListener("input", () => { reason.style.height = "auto"; reason.style.height = `${reason.scrollHeight}px`; });
    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (busy || !options || !form.reportValidity()) return;
        if (!rows.children.length) { message.textContent = "En az bir ders satırı ekleyin."; return; }
        const data = new FormData(form); busy = true; send.disabled = true; add.disabled = true; message.textContent = "";
        try {
            const response = await fetch(form.action, { method: "POST", credentials: "same-origin", body: data });
            if (response.ok && !response.redirected) { window.location.reload(); return; }
            message.textContent = [400, 409, 503].includes(response.status) ? await response.text() : "Oturum veya aktarım yetkisi doğrulanamadı.";
        } catch { message.textContent = "Sonuç doğrulanamadı. Tekrar göndermeden önce dönem kayıtlarını kontrol edin."; }
        finally { busy = false; send.disabled = false; add.disabled = false; }
    });
});
