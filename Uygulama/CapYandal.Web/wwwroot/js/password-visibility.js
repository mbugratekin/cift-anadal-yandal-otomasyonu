"use strict";
// Yalnız kullanıcının açık seçimiyle göster; değerleri kaydetme, gönderme veya loglama.
document.querySelectorAll("[data-password-visibility]").forEach(label => {
    const toggle = label.querySelector('input[type="checkbox"]');
    const form = label.closest("form");
    if (!toggle || !form) return;
    const fields = (toggle.getAttribute("aria-controls") || "").split(/\s+/)
        .map(id => document.getElementById(id)).filter(field => field && form.contains(field));
    if (!fields.length) return;
    const update = () => fields.forEach(field => { field.type = toggle.checked ? "text" : "password"; });
    const hide = () => { toggle.checked = false; update(); };
    toggle.addEventListener("change", update);
    form.addEventListener("submit", hide);
    window.addEventListener("pagehide", hide);
    window.addEventListener("pageshow", hide);
    document.addEventListener("visibilitychange", () => { if (document.hidden) hide(); });
    hide();
    label.hidden = false;
});
