(() => {
    // Hesap formları — sonuç doğrulanana kadar çift gönderim ve otomatik tekrar engellenir.
    let busy = false;
    document.addEventListener('submit', async event => {
        const form = event.target;
        if (!form.matches('.personel-hesap-form')) return;
        event.preventDefault();
        if (busy || form.dataset.saved || !form.reportValidity()) return;
        const body = new FormData(form);
        const controls = [...form.querySelectorAll('input,select,button')];
        const states = controls.map(control => control.disabled);
        const message = form.querySelector('[role=status]');
        busy = true;
        controls.forEach(control => control.disabled = true);
        try {
            const response = await fetch(form.action, {
                method: 'POST', body, credentials: 'same-origin', headers: { Accept: 'application/json' }
            });
            const result = await response.json().catch(() => ({}));
            if (!response.ok || result.kaydedildi !== true)
                throw new Error(result.hata || 'İşlem sonucu doğrulanamadı; yeniden göndermeden sayfayı yenileyin.');
            form.dataset.saved = 'true';
            message.textContent = result.bilgi;
        } catch (error) {
            message.textContent = error.message;
        } finally {
            busy = false;
            if (!form.dataset.saved) controls.forEach((control, index) => control.disabled = states[index]);
        }
    });
})();
