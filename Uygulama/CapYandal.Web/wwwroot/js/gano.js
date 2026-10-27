(() => {
    "use strict";
    function fit(field) { field.style.height = "auto"; field.style.height = `${field.scrollHeight}px`; }
    document.addEventListener("input", event => {
        if (event.target.matches(".gano-form textarea")) fit(event.target);
        if (event.target.matches(".gano-form [name=AnaDalGano]")) {
            const source = event.target.form.elements.namedItem("AnaDalKaynak");
            source.required = event.target.value.trim() !== "";
        }
    });
    document.addEventListener("change", event => {
        if (!event.target.matches(".gano-form [name=Donem]")) return;
        const edit = [...document.querySelectorAll(".gano-edit")].find(button => button.dataset.donem === event.target.value);
        edit?.click();
    });
    document.addEventListener("click", event => {
        const button = event.target.closest(".gano-edit, .gano-new");
        if (!button) return;
        const form = document.querySelector(".gano-form");
        if (!form || form.dataset.busy) return;
        form.reset();
        const editing = button.classList.contains("gano-edit");
        for (const [name, data] of [["Donem", "donem"], ["AnaDalGano", "ana"], ["ProgramGano", "program"], ["AnaDalSurum", "anaSurum"], ["ProgramSurum", "programSurum"], ["AnaDalKaynak", "anaKaynak"]]) {
            form.elements.namedItem(name).value = editing ? (button.dataset[data] || "") : "";
        }
        if (!editing) form.elements.namedItem("Donem").value = form.dataset.nextPeriod || "";
        form.querySelector(".academic-message").textContent = "";
        const period = form.elements.namedItem('Donem');
        [...period.options].forEach(option => { option.disabled = editing && option.value !== period.value; });
        form.elements.namedItem("Gerekce").required = editing;
        form.elements.namedItem("AnaDalKaynak").required = form.elements.namedItem("AnaDalGano").value.trim() !== "";
        form.querySelector('.gano-reason-field').hidden = !editing;
        form.querySelector('.gano-new').hidden = !editing;
        form.querySelector('button[type=submit]').textContent = editing ? 'Düzeltmeyi kaydet' : 'GANO kaydet';
        fit(form.elements.namedItem("Gerekce"));
        form.elements.namedItem(editing ? "AnaDalGano" : "Donem").focus();
    });
    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!form.matches(".academic-save-form")) return;
        event.preventDefault();
        if (form.dataset.busy) return;
        const message = form.querySelector(".academic-message");
        const fields = new FormData(form);
        form.dataset.busy = "1";
        const controls = [...form.querySelectorAll("input, textarea, select, button")];
        const states = controls.map(control => control.disabled);
        controls.forEach(control => { control.disabled = true; });
        message.textContent = "";
        try {
            const response = await fetch(form.action, { method: "POST", body: fields, credentials: "same-origin", headers: { "Accept": "application/json" } });
            const body = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(body.hata || "Kayıt tamamlanamadı. Girişiniz korunuyor.");
            const detail = await fetch(location.href, { credentials: "same-origin", cache: "no-store" });
            if (!detail.ok) throw new Error("Kaydedildi; güncel liste için sayfayı yenileyin.");
            const documentCopy = new DOMParser().parseFromString(await detail.text(), "text/html");
            const updated = documentCopy.querySelector("#program-takibi-basligi")?.closest("section");
            const old = document.querySelector("#program-takibi-basligi")?.closest("section");
            if (!updated || !old) throw new Error("Kaydedildi; güncel liste için sayfayı yenileyin.");
            old.replaceWith(updated);
            document.dispatchEvent(new Event('academic-record-saved'));
            updated.querySelector(".academic-message").textContent = "Kaydedildi.";
            updated.querySelector("#gano-donem").focus();
        } catch (error) {
            message.textContent = error.message || "Kayıt tamamlanamadı. Girişiniz korunuyor.";
        } finally {
            delete form.dataset.busy;
            controls.forEach((control, index) => { control.disabled = states[index]; });
        }
    });
})();
