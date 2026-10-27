"use strict";
document.querySelectorAll(".registration-period").forEach(form => {
    const originalFields = [...form.querySelectorAll("input,select")].map(field => [field, field.value]);
    form.addEventListener("submit", event => {
        if (!document.dispatchEvent(new CustomEvent("academic-navigation", { cancelable: true }))) {
            event.preventDefault(); originalFields.forEach(([field, value]) => { field.value = value; });
        }
    });
    form.querySelectorAll("input,select").forEach(field => field.addEventListener("change", () => form.requestSubmit()));
});
document.querySelectorAll(".registration-command").forEach(form => {
    let busy = false, confirmation = false;
    const message = form.parentElement.querySelector(".registration-message");
    const clear = form.querySelector(".registration-clear");
    const apply = form.querySelector('button[type="submit"]');
    const start = form.elements.namedItem("Baslangic"), end = form.elements.namedItem("Bitis");
    const closing = form.elements.namedItem("Niyet").value === "2";
    function updateButton() {
        const immediate = closing ? !end.value : !start?.value;
        apply.textContent = confirmation ? "Onayla" : closing ? "Ders Kaydını Kapat" : "Ders Kaydını Aç";
        apply.classList.toggle("immediate-close", closing && immediate);
    }
    function cancelConfirmation() { confirmation = false; updateButton(); }
    form.querySelectorAll('input[type="datetime-local"]').forEach(input => input.addEventListener("input", cancelConfirmation));
    document.addEventListener("keydown", event => { if (event.key === "Escape" && confirmation) cancelConfirmation(); });
    document.addEventListener("click", event => { if (confirmation && !apply.contains(event.target)) cancelConfirmation(); });
    updateButton();
    clear.addEventListener("click", () => {
        if (busy) return;
        form.querySelectorAll('input[type="datetime-local"]').forEach(input => { input.value = ""; });
        message.textContent = ""; // Yalnız girişleri temizler; DB planını iptal etmez.
        cancelConfirmation();
    });
    form.addEventListener("submit", async event => {
        event.preventDefault(); if (busy || !document.dispatchEvent(new CustomEvent("academic-navigation", { cancelable: true }))) return;
        const period = form.parentElement.querySelector(".registration-period");
        if (period && (!period.checkValidity() || period.elements.namedItem("yil").value !== form.elements.namedItem("Yil").value ||
            period.elements.namedItem("yariyil").value !== form.elements.namedItem("Yariyil").value)) {
            message.textContent = "Geçerli yıl ve dönemi önce seçin."; return;
        }
        const immediate = closing ? !end.value : !start?.value;
        if (immediate && !confirmation) { confirmation = true; updateButton(); return; }
        confirmation = false;
        busy = true; message.textContent = "";
        const data = new FormData(form);
        apply.disabled = true; clear.disabled = true;
        try {
            const response = await fetch(form.action, { method: "POST", body: data, credentials: "same-origin" });
            if (response.ok) { window.location.assign(response.url); return; }
            message.textContent = response.status === 409 ? "Kayıt durumu değişti; sayfayı yenileyin."
                : response.status === 400 ? "Tarih ve saatleri kontrol edin; bitiş başlangıçtan sonra olmalı."
                : "İşlem tamamlanamadı; sayfayı yenileyin. Otomatik tekrar yapılmadı.";
        } catch (_) { message.textContent = "Bağlantı kesildi. Durumu kontrol etmek için sayfayı yenileyin; otomatik tekrar yapılmadı."; }
        finally { busy = false; apply.disabled = false; clear.disabled = false; updateButton(); }
    });
});
