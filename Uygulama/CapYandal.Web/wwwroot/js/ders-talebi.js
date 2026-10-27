"use strict";
// Ders talebi — sabit istek kimliği korunur; belirsiz yanıtta otomatik tekrar yoktur.
document.querySelectorAll(".ders-talebi-form").forEach(form => {
    let busy = false;
    const field = form.elements.Aciklama, message = form.querySelector('[role="alert"]'), button = form.querySelector('button[type="submit"]');
    field.addEventListener("input", () => { field.style.height = "auto"; field.style.height = `${field.scrollHeight}px`; });
    form.addEventListener("submit", async event => {
        event.preventDefault(); if (busy || !form.reportValidity()) return;
        const data = new FormData(form); busy = true; button.disabled = true; message.textContent = "";
        try {
            const response = await fetch(form.action, { method: "POST", body: data, credentials: "same-origin" });
            if (response.ok && !response.redirected) { window.location.assign(form.dataset.doneUrl); return; }
            message.textContent = [400, 409, 503].includes(response.status) ? await response.text() : "Oturum veya talep yetkisi doğrulanamadı.";
        } catch { message.textContent = "Sonuç doğrulanamadı. Tekrar göndermeden önce sohbeti kontrol edin."; }
        finally { busy = false; button.disabled = false; }
    });
});
