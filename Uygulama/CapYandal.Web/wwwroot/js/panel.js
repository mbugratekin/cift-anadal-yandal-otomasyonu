"use strict";
document.addEventListener('keydown', event => { if (event.key === 'Tab') document.body.classList.add('keyboard-navigation'); });
document.addEventListener('pointerdown', () => document.body.classList.remove('keyboard-navigation'));
function hideSelectHeadings() {
    document.querySelectorAll('select').forEach(select => {
        const first = select.options[0];
        if (first && !first.value && /seçin$/iu.test(first.text.trim())) first.hidden = first.disabled = true;
    });
}
hideSelectHeadings();
if (location.hash.startsWith('#mufredat-')) {
    let target=document.getElementById(location.hash.slice(1));
    while(target){if(target.matches('details'))target.open=true;target=target.parentElement;}
}
new MutationObserver(records => { if (records.some(r => r.addedNodes.length)) hideSelectHeadings(); }).observe(document.querySelector('main'), { childList:true, subtree:true });
const adresFormu = document.getElementById("personal-email-form");
if (adresFormu) {
    const alan = document.getElementById("personal-email");
    const durum = document.getElementById("personal-email-status");
    const dogrulama = document.getElementById("personal-email-verification");
    let kayitli = alan.value.trim();
    let isleniyor = false;
    let hataVar = false;
    let renkZamani;
    function hatayiGoster(mesaj) {
        alan.value = "";
        alan.placeholder = mesaj;
        alan.classList.add("email-save-error");
        alan.setAttribute("aria-invalid", "true");
        durum.textContent = mesaj;
        hataVar = true;
    }
    async function kaydet() {
        const adres = alan.value.trim();
        if (isleniyor || hataVar || adres === kayitli) return;
        clearTimeout(renkZamani);
        alan.classList.remove("email-save-success");
        if (!adres || !alan.checkValidity()) { hatayiGoster("Geçerli bir e-posta adresi yazın."); return; }
        isleniyor = true;
        alan.readOnly = true;
        try {
            const veri = new FormData(adresFormu);
            veri.set("KisiselEposta", adres);
            const yanit = await fetch(adresFormu.action, { method: "POST", body: veri, credentials: "same-origin", headers: { Accept: "application/json" } });
            if (yanit.redirected || !yanit.headers.get("content-type")?.includes("application/json")) throw new Error();
            const sonuc = await yanit.json();
            if (!yanit.ok) { hatayiGoster(sonuc.hata || "Kaydedilemedi. Tekrar deneyin."); return; }
            kayitli = adres;
            alan.value = adres;
            if (sonuc.degisti) {
                alan.classList.add("email-save-success");
                durum.textContent = "Adres kaydedildi; doğrulama bekleniyor.";
                dogrulama.textContent = sonuc.dogrulamaGonderildi ? "Doğrulanmadı" : "Doğrulanmadı — doğrulama iletisi gönderilemedi.";
                renkZamani = setTimeout(() => alan.classList.remove("email-save-success"), 3000);
            }
        } catch { hatayiGoster("Kaydedilemedi. Bağlantınızı kontrol edin."); }
        finally { isleniyor = false; alan.readOnly = false; }
    }
    function hatayiTemizle() {
        if (!hataVar) return;
        hataVar = false;
        alan.placeholder = "Kişisel e-posta adresiniz";
        alan.classList.remove("email-save-error");
        alan.removeAttribute("aria-invalid");
        durum.textContent = "";
    }
    alan.addEventListener("focus", hatayiTemizle);
    alan.addEventListener("input", hatayiTemizle);
    alan.addEventListener("blur", kaydet);
    adresFormu.addEventListener("submit", event => { event.preventDefault(); kaydet(); });
}
document.querySelectorAll(".student-detail-row").forEach(row => {
    row.addEventListener("click", event => {
        if (document.querySelector('.student-filters')) return;
        if (event.target.closest("a, button, input, select, textarea")) return;
        row.querySelector(".student-detail-link")?.click();
    });
});
const account = document.querySelector(".panel-account");
document.addEventListener("click", event => {
    if (account?.open && !account.contains(event.target)) account.open = false;
});
document.addEventListener("keydown", event => {
    if (event.key === "Escape" && account?.open) {
        account.open = false;
        account.querySelector("summary").focus();
    }
});
const photoInput = document.getElementById("photo-input");
if (photoInput) {
    const preview = document.getElementById("photo-preview");
    const confirmation = document.getElementById("photo-confirmation");
    const error = document.getElementById("photo-error");
    let objectUrl = null;
    let selection = 0;
    let ready = false;
    document.getElementById("photo-select").addEventListener("click", () => photoInput.click());
    photoInput.addEventListener("change", async () => {
        const current = ++selection;
        ready = false;
        confirmation.hidden = true;
        confirmation.querySelector("input").checked = false;
        error.textContent = "";
        preview.hidden = true;
        document.getElementById("photo-placeholder").removeAttribute("hidden");
        document.getElementById("photo-select").classList.remove("has-photo-preview");
        document.getElementById("photo-select-label").hidden = false;
        if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
        const file = photoInput.files[0];
        if (!file) return;
        if (!/\.(jpe?g|png)$/i.test(file.name) || !["image/jpeg", "image/png"].includes(file.type) || file.size > 1024 * 1024) {
            error.textContent = "En fazla 1 MB büyüklüğünde JPG veya PNG fotoğraf seçin.";
            photoInput.value = "";
            return;
        }
        const url = URL.createObjectURL(file);
        objectUrl = url;
        const probe = new Image();
        probe.src = url;
        try {
            await probe.decode();
            if (current !== selection) return;
            if (probe.naturalWidth < 300 || probe.naturalHeight < 400 || probe.naturalWidth > 4096
                || probe.naturalHeight > 4096 || probe.naturalWidth * probe.naturalHeight > 12000000)
                throw new Error("Fotoğraf en az 300×400, en fazla 4096×4096 piksel ve 12 milyon piksel olmalıdır.");
            preview.src = url;
            preview.hidden = false;
            document.getElementById("photo-placeholder").setAttribute("hidden", "");
            document.getElementById("photo-select").classList.add("has-photo-preview");
            document.getElementById("photo-select-label").hidden = true;
            confirmation.hidden = false;
            ready = true;
        } catch (exception) {
            if (current !== selection) return;
            error.textContent = exception instanceof Error && exception.message.startsWith("Fotoğraf")
                ? exception.message : "Fotoğraf okunamadı. Başka bir dosya seçin.";
            photoInput.value = "";
            URL.revokeObjectURL(url);
            objectUrl = null;
        }
    });
    document.getElementById("photo-form").addEventListener("submit", event => {
        if (!ready || !photoInput.files.length) {
            event.preventDefault();
            error.textContent = "Önce geçerli bir fotoğraf seçin.";
        }
    });
    window.addEventListener("pagehide", () => { if (objectUrl) URL.revokeObjectURL(objectUrl); });
}
