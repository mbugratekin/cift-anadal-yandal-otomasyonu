"use strict";
// Uygulama içi bildirim — görünür sayfada yalnız yetkili sohbet sayısı okunur; e-posta gönderilmez.
(() => {
    const badge = document.querySelector(".sohbet-bildirim"); if (!badge) return;
    let busy = false, queued = false;
    async function update() {
        if (document.hidden) return;
        if (busy) { queued = true; return; } busy = true;
        try {
            const response = await fetch(badge.dataset.url, { credentials: "same-origin", cache: "no-store" });
            if (!response.ok || response.redirected) { badge.hidden = true; return; }
            const data = await response.json(); const count = data.toplam;
            if (!Number.isInteger(count) || count < 0) { badge.hidden = true; return; }
            badge.textContent = String(count); badge.hidden = count === 0;
            badge.setAttribute("aria-label", count + " okunmamış mesaj");
        } catch { badge.hidden = true; } finally { busy = false; if (queued) { queued = false; void update(); } }
    }
    update(); const timer = setInterval(update, 30000);
    document.addEventListener("sohbet-okundu", update);
    document.addEventListener("visibilitychange", update);
    window.addEventListener("pagehide", () => clearInterval(timer), { once: true });
})();
