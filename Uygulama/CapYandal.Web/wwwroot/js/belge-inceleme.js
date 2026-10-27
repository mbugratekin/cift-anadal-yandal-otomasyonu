(() => {
    'use strict';
    if (window.__belgeIncelemeBound) return;
    window.__belgeIncelemeBound = true;
    const active = new WeakSet();
    // İstek ve inceleme — formun CSRF verisi korunur; hata metni HTML olarak yorumlanmaz.
    document.addEventListener('submit', async event => {
        const form = event.target;
        if (!form.matches('.belge-review-form,.belge-request-form')) return;
        event.preventDefault();
        if (active.has(form) || !form.reportValidity()) return;
        const data = new FormData(form), button = form.querySelector('button[type=submit]');
        const status = form.querySelector('[role=status]');
        active.add(form); button.disabled = true; status.textContent = '';
        try {
            const response = await fetch(form.action, {method:'POST',credentials:'same-origin',body:data});
            if (response.redirected) throw new Error('Oturumunuzu kontrol edin; girişleriniz korunuyor.');
            const result = await response.json();
            if (!response.ok || !result.kaydedildi) throw new Error(result.hata || 'İşlem kaydedilemedi.');
            location.reload();
        } catch(error) {status.textContent = error instanceof Error ? error.message : 'İşlem kaydedilemedi.';}
        finally {active.delete(form);button.disabled=false;}
    });
    const grow = input => {input.style.resize='none';input.style.height='auto';input.style.height=input.scrollHeight+'px';};
    document.querySelectorAll('.belge-review-form textarea,.belge-request-form textarea,.evrak-upload-form textarea').forEach(grow);
    document.addEventListener('input', event => {if(event.target.matches('.belge-review-form textarea,.belge-request-form textarea,.evrak-upload-form textarea'))grow(event.target);});
})();
