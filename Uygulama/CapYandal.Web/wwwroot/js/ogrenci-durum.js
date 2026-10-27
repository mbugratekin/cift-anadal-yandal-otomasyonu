(() => {
    "use strict";
    const url = document.currentScript?.dataset.durumUrl;
    if (!url) return;
    let baseline, busy = false, dirty = false;
    const markDirty = event => { if (event.target.closest("form")) dirty = true; };
    document.addEventListener("input", markDirty);
    document.addEventListener("change", markDirty);
    async function read() {
        if (busy || document.hidden || dirty || document.querySelector("dialog[open]")) return;
        busy = true;
        try {
            const response = await fetch(url, { credentials: "same-origin", cache: "no-store", headers: { Accept: "application/json" } });
            if (!response.ok || response.redirected) return;
            const signature = JSON.stringify(await response.json());
            if (baseline !== undefined && signature !== baseline) { location.reload(); return; }
            baseline = signature;
        } catch { /* Bağlantı hatası oturumu kapatmaz; sonraki turda yeniden okunur. */ }
        finally { busy = false; }
    }
    void read();
    setInterval(read, 5000);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) void read(); });
})();
