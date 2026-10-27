(() => {
    'use strict';
    // Manuel karar — CSRF formuyla gönderilir; hata halinde gerekçe ve seçim korunur.
    const form = document.querySelector('.program-decision-form');
    if (!form) return;
    const input = form.querySelector('textarea');
    const confirmation = form.querySelector('input[name="UyariSonrasiTekrar"]');
    const button = form.querySelector('button[type=submit]');
    const message = form.querySelector('.program-decision-message');
    const resetWarning = () => {
        confirmation.value = 'false';
        button.textContent = 'Kararı kaydet';
        message.textContent = '';
    };
    form.querySelector('select[name="Karar"]').addEventListener('change', resetWarning);
    input.addEventListener('input', resetWarning);
    input.style.resize = 'none';
    const grow = () => { input.style.height = 'auto'; input.style.height = `${input.scrollHeight}px`; };
    input.addEventListener('input', grow);
    grow();
    let busy = false;
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy || !form.reportValidity()) return;
        busy = true;
        button.disabled = true;
        message.textContent = '';
        try {
            const response = await fetch(form.action, { method: 'POST', credentials: 'same-origin', body: new FormData(form) });
            if (response.redirected) throw new Error('Oturumunuz yenilenmeli. Gerekçenizi koruyup yeniden giriş yapın.');
            const result = await response.json();
            if (response.status === 409 && result.tekrarGonder && result.uyari) {
                confirmation.value = 'true';
                button.textContent = 'Kararı tekrar kaydet';
                message.textContent = result.uyari;
                return;
            }
            if (!response.ok) throw new Error(result.hata || 'Karar kaydedilemedi.');
            location.reload();
        } catch (error) {
            message.textContent = error instanceof Error ? error.message : 'Karar kaydedilemedi.';
        } finally { button.disabled = false; busy = false; }
    });
})();
