(() => {
    if (window.__evrakUiBound) return;
    window.__evrakUiBound = true;
    const maxBytes = 10 * 1024 * 1024;
    const active = new WeakSet();

    function sizeText(bytes) {
        if (bytes < 1024 * 1024) return Math.max(1, Math.round(bytes / 1024)) + " KB";
        return (bytes / (1024 * 1024)).toLocaleString("tr-TR", { maximumFractionDigits: 1 }) + " MB";
    }

    function message(form, text) {
        const target = form.querySelector("[data-evrak-upload-message]");
        if (target) target.textContent = text;
    }

    function chooseFile(picker, files) {
        const input = picker.querySelector("[data-evrak-file-input]");
        const name = picker.querySelector("[data-evrak-file-name]");
        const remove = picker.querySelector("[data-evrak-file-remove]");
        const status = picker.querySelector("[data-evrak-file-message]");
        const list = [...(files || [])];
        let error = "";
        if (list.length === 0) { status.textContent = ''; return false; }
        if (list.length !== 1) error = "Bir seferde tek PDF veya Word dosyası seçin.";
        else {
            const file = list[0], extension = file.name.split(".").pop().toLowerCase();
            if (!["pdf", "docx"].includes(extension)) error = "Yalnızca PDF veya Word (.docx) dosyası seçin.";
            else if (!file.size) error = "Boş dosya gönderilemez.";
            else if (file.size > maxBytes) error = "Dosya 10 MB sınırını aşıyor.";
        }
        if (error) {
            input.value = "";
            name.textContent = picker.dataset.defaultText;
            remove.hidden = true;
            status.textContent = error;
            return false;
        }
        const file = list[0];
        name.textContent = file.name + " · " + sizeText(file.size);
        remove.hidden = false;
        status.textContent = "";
        return true;
    }

    document.addEventListener("change", event => {
        const input = event.target.closest("[data-evrak-file-input]");
        if (input) chooseFile(input.closest("[data-evrak-file-picker]"), input.files);
    });

    document.addEventListener("click", event => {
        const remove = event.target.closest("[data-evrak-file-remove]");
        if (!remove) return;
        const picker = remove.closest("[data-evrak-file-picker]");
        const input = picker.querySelector("[data-evrak-file-input]");
        input.value = "";
        picker.querySelector("[data-evrak-file-name]").textContent = picker.dataset.defaultText;
        picker.querySelector("[data-evrak-file-message]").textContent = "";
        remove.hidden = true;
        input.focus();
    });

    document.addEventListener("dragover", event => {
        const zone = event.target.closest("[data-evrak-drop-zone]");
        if (zone) { event.preventDefault(); zone.dataset.dragging = "true"; }
    });
    document.addEventListener("dragleave", event => {
        const zone = event.target.closest("[data-evrak-drop-zone]");
        if (zone && !zone.contains(event.relatedTarget)) delete zone.dataset.dragging;
    });
    document.addEventListener("drop", event => {
        const zone = event.target.closest("[data-evrak-drop-zone]");
        if (!zone) return;
        event.preventDefault();
        delete zone.dataset.dragging;
        const picker = zone.closest("[data-evrak-file-picker]");
        const input = picker.querySelector("[data-evrak-file-input]");
        const files = event.dataTransfer?.files;
        if (!files?.length) return;
        const transfer = new DataTransfer();
        [...files].forEach(file => transfer.items.add(file));
        input.files = transfer.files;
        chooseFile(picker, input.files);
    });

    // Belge yükleme — yeni sürüm/istek alanları CSRF formuyla taşınır; belirsiz sonuç tekrar gönderilmez.
    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!form.matches(".evrak-upload-form")) return;
        event.preventDefault();
        if (active.has(form) || form.dataset.saved === "true" || form.dataset.unknown === "true" || !form.reportValidity()) return;
        if(!form.querySelector('[data-evrak-file-input]').files.length) { message(form,'Belge yüklemek için dosya seçin.'); return; }
        if (!chooseFile(form.querySelector('[data-evrak-file-picker]'), form.querySelector('[data-evrak-file-input]').files)) return;
        const controls = [...form.querySelectorAll("input,button,select,textarea")];
        const disabled = controls.map(control => control.disabled);
        const body = new FormData(form);
        active.add(form); controls.forEach(control => { control.disabled = true; });
        try {
            message(form,'');
            const response = await new Promise((resolve,reject) => {
                const request=new XMLHttpRequest(); request.open('POST',form.action); request.setRequestHeader('Accept','application/json');
                request.onload=()=>resolve(new Response(request.responseText,{status:request.status,headers:{'Content-Type':request.getResponseHeader('Content-Type')||'text/plain'}}));
                request.onerror=reject;request.onabort=reject;request.send(body);
            });
            if (response.redirected) throw new Error("Oturumunuzu kontrol edin; dosya otomatik tekrar gönderilmedi.");
            const result = await response.json();
            if (!response.ok || result.kaydedildi !== true) {
                if (response.status >= 500) form.dataset.unknown = "true";
                throw new Error(result.hata || "Belge yüklenemedi; sayfayı yenileyip durumu kontrol edin.");
            }
            form.dataset.saved = "true"; message(form,"");
            location.reload();
        } catch(error) {
            if (!(error instanceof Error) || error.name === "TypeError" || error.name === "SyntaxError") form.dataset.unknown = "true";
            message(form,error instanceof Error ? error.message : "Gönderim sonucu doğrulanamadı; otomatik tekrar yapılmadı.");
        } finally {
            active.delete(form);
            if (form.dataset.saved !== "true" && form.dataset.unknown !== "true") controls.forEach((control,index) => { control.disabled = disabled[index]; });
        }
    });
})();
