(() => {
    "use strict";
    const previousNumbers = new WeakMap();
    const numericField = input => input.matches(".template-form input[data-field='Kod'], .template-form input[data-field='Kredi'], .template-form input[data-field='Akts'], .template-form input[data-field='MufredatYariyili']");
    const numericValue = (input, value) => input.dataset.field === "Kod"
        ? /^[0-9]*$/.test(value) : input.dataset.field === "MufredatYariyili" ? /^[1-8]?$/.test(value) : /^[0-9]*(?:[.,][0-9]{0,2})?$/.test(value);
    document.addEventListener("focusin", event => {
        const input = event.target;
        if (input instanceof HTMLInputElement && numericField(input)) previousNumbers.set(input, input.value);
    });
    document.addEventListener("beforeinput", event => {
        const input = event.target;
        if (!(input instanceof HTMLInputElement) || !numericField(input) || event.data == null) return;
        // Sayı alanı değişince değeri yeniden kontrol eder.
        if (input.dataset.field === "MufredatYariyili") return;
        const next = input.value.slice(0, input.selectionStart ?? input.value.length) + event.data +
            input.value.slice(input.selectionEnd ?? input.value.length);
        if (!numericValue(input, next)) event.preventDefault();
    });
    document.addEventListener("input", event => {
        const input = event.target;
        if (!(input instanceof HTMLInputElement) || !numericField(input)) return;
        if (numericValue(input, input.value)) previousNumbers.set(input, input.value);
        else input.value = previousNumbers.get(input) ?? input.defaultValue;
        if (input.dataset.field === "MufredatYariyili") updateBand(input.closest("tr"));
    });
    const reindex = form => form.querySelectorAll(".template-rows .template-row").forEach((row, index) => {
        row.querySelectorAll("[data-field]").forEach(input => { input.name = `Dersler[${index}].${input.dataset.field}`; });
    });
    const updateBand = row => {
        const value = Number(row.querySelector("[data-field='MufredatYariyili']").value);
        row.dataset.semesterBand = Number.isInteger(value) && value >= 1 && value <= 8 ? value % 2 ? "odd" : "even" : "";
    };
    const sortRows = form => {
        const body = form.querySelector(".template-rows");
        [...body.querySelectorAll(".template-row")].sort((a, b) =>
            (Number(a.querySelector("[data-field='MufredatYariyili']").value) || 99) -
            (Number(b.querySelector("[data-field='MufredatYariyili']").value) || 99))
            .forEach(row => { updateBand(row); body.append(row); });
        reindex(form);
    };
    document.addEventListener("change", event => {
        if (event.target.matches(".template-form [data-field='MufredatYariyili']")) sortRows(event.target.closest("form"));
    });
    const hasDrafts = () => [...document.querySelectorAll(".template-form")].some(form =>
        form.dataset.busy || form.querySelector(".template-row[data-new]") ||
        [...form.elements].filter(input => input.matches("input, select")).some(input => input.value !== (input instanceof HTMLSelectElement ? [...input.options].find(option => option.defaultSelected)?.value ?? input.options[0]?.value : input.defaultValue)));
    document.addEventListener("change", event => {
        if (!event.target.matches(".template-kind")) return;
        const form = event.target.closest("form");
        const source = form.querySelector("[name='KaynakAnaDal']");
        const shared = event.target.value === "2";
        source.disabled = shared; source.required = !shared;
        form.querySelector(".template-source-label").hidden = shared;
    });
    document.addEventListener("click", event => {
        if (event.target.closest(".template-heading-input")) event.stopPropagation();
    });
    document.addEventListener("keydown", event => {
        if (event.target.closest(".template-heading-input")) {
            event.stopPropagation();
            if (event.key === "Enter") {
                event.preventDefault();
                const form = event.target.form || event.target.closest(".template-list")?.querySelector(".template-form");
                if (form) form.requestSubmit();
            }
        }
    });
    document.addEventListener("click", event => {
        const button = event.target.closest(".template-open");
        if (!button) return;
        const details = [...document.querySelectorAll(".template-list")].find(item => item.dataset.templateId === button.dataset.templateId);
        if (details) { details.open = true; details.querySelector("summary").focus(); details.scrollIntoView({ block: "nearest" }); }
    });
    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!form.matches(".template-management-form")) return;
        event.preventDefault();
        const message = form.querySelector(".template-message");
        const workspace = form.closest(".student-programs");
        if (workspace.dataset.busy) return;
        if (hasDrafts()) { message.textContent = "Önce açık ders değişikliklerini Kaydet veya Vazgeç ile tamamlayın."; return; }
        // Tüm şablonlar yenilenirken diğer formdaki kaydedilmemiş bölüm adı da korunur.
        const create = workspace.querySelector(".template-create");
        if (form !== create && (create.querySelector("[name='KaynakAnaDal']").value.trim() || create.querySelector(".template-kind").value !== "1")) {
            message.textContent = "Önce yeni şablon girişini tamamlayın veya bölüm alanını boşaltın."; return;
        }
        const fields = new FormData(form);
        const controls = [...workspace.querySelectorAll("input, select, button")];
        const disabled = controls.map(control => control.disabled);
        workspace.dataset.busy = "1"; controls.forEach(control => { control.disabled = true; });
        message.textContent = "";
        try {
            const response = await fetch(form.action, { method: "POST", body: fields, credentials: "same-origin", headers: { Accept: "application/json" } });
            const body = await response.json().catch(() => ({}));
            if (!response.ok || body.kaydedildi !== true) throw new Error(body.hata || "İşlem tamamlanamadı; girişiniz korunuyor.");
            const page = await fetch(location.href, { credentials: "same-origin", cache: "no-store" });
            if (!page.ok) throw new Error("Kaydedildi; güncel liste için sayfayı yenileyin.");
            const copy = new DOMParser().parseFromString(await page.text(), "text/html");
            const updated = copy.querySelector(".student-programs");
            if (!updated) throw new Error("Kaydedildi; güncel liste için sayfayı yenileyin.");
            workspace.replaceWith(updated);
            updated.querySelector(".template-create .template-message").textContent = "Kaydedildi.";
            updated.querySelector(".template-kind").focus();
        } catch (error) { message.textContent = error.message || "İşlem tamamlanamadı; girişiniz korunuyor."; }
        finally { delete workspace.dataset.busy; controls.forEach((control, index) => { control.disabled = disabled[index]; }); }
    });
    document.addEventListener("click", event => {
        const button = event.target.closest(".template-add, .template-remove, .template-reset");
        if (!button) return;
        const form = button.closest(".template-form");
        if (!form || form.dataset.busy) return;
        if (button.classList.contains("template-add")) {
            const incomplete = [...form.querySelectorAll(".template-row:not([hidden]) [required]")]
                .find(input => !input.value.trim() || !input.validity.valid);
            if (incomplete) { incomplete.focus(); return; }
            if (form.querySelectorAll(".template-row").length >= 500) return;
            const fragment = form.querySelector(".template-new-row").content.cloneNode(true);
            const row = fragment.querySelector("tr");
            form.querySelector(".template-rows").append(fragment);
            reindex(form); row.querySelector("[data-field='Kod']").focus();
        } else if (button.classList.contains("template-remove")) {
            const row = button.closest("tr");
            if (row.dataset.new) row.remove();
            else {
                row.hidden = true; row.querySelector("[data-field='Cikar']").value = "true";
                row.querySelectorAll("[required]").forEach(input => { input.required = false; input.dataset.wasRequired = "1"; });
            }
            reindex(form); form.querySelector(".template-add").focus();
        } else {
            form.reset();
            form.querySelectorAll(".template-row[data-new]").forEach(row => row.remove());
            form.querySelectorAll(".template-row").forEach(row => {
                row.hidden = false;
                row.querySelectorAll("[data-was-required]").forEach(input => { input.required = true; delete input.dataset.wasRequired; });
            });
            sortRows(form);
        }
        form.querySelector(".template-message").textContent = "";
    });
    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!form.matches(".template-form")) return;
        event.preventDefault();
        if (form.dataset.busy) return;
        reindex(form);
        const fields = new FormData(form);
        const message = form.querySelector(".template-message");
        const controls = [...form.elements].filter(control => control.matches("input, select, button"));
        const disabled = controls.map(control => control.disabled);
        form.dataset.busy = "1"; controls.forEach(control => { control.disabled = true; });
        message.textContent = "";
        try {
            const response = await fetch(form.action, { method: "POST", body: fields, credentials: "same-origin", headers: { Accept: "application/json" } });
            const body = await response.json().catch(() => ({}));
            if (!response.ok || body.kaydedildi !== true) throw new Error(body.hata || "Kaydedilemedi; değişiklikleriniz korunuyor.");
            const page = await fetch(location.href, { credentials: "same-origin", cache: "no-store" });
            if (!page.ok) throw new Error("Kaydedildi; güncel liste için sayfayı yenileyin.");
            const copy = new DOMParser().parseFromString(await page.text(), "text/html");
            const old = form.closest(".template-list");
            const updated = [...copy.querySelectorAll(".template-list")].find(item => item.dataset.templateId === old.dataset.templateId);
            if (!updated) throw new Error("Kaydedildi; güncel liste için sayfayı yenileyin.");
            updated.open = true; old.replaceWith(updated);
            const managerRow = [...document.querySelectorAll("[data-managed-template-id]")].find(row => row.dataset.managedTemplateId === updated.dataset.templateId);
            const refreshedRow = [...copy.querySelectorAll("[data-managed-template-id]")].find(row => row.dataset.managedTemplateId === updated.dataset.templateId);
            if (managerRow && refreshedRow) managerRow.replaceWith(refreshedRow);
            updated.querySelector(".template-message").textContent = "Kaydedildi.";
            updated.querySelector(".template-add").focus();
        } catch (error) { message.textContent = error.message || "Kaydedilemedi; değişiklikleriniz korunuyor."; }
        finally { delete form.dataset.busy; controls.forEach((control, index) => { control.disabled = disabled[index]; }); }
    });
})();
