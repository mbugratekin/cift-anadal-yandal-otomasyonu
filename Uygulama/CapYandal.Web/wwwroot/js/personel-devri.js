(() => {
    let busy = false;
    function reset(form) {
        delete form.dataset.confirm;
        form.querySelector('button[type=submit]').textContent = 'Görevi devret';
        form.querySelector('.personel-devir-message').textContent = '';
    }
    document.addEventListener('click', event => {
        const button = event.target.closest('.personel-devir-open');
        if (button && !busy) {
            const row = document.getElementById(button.getAttribute('aria-controls'));
            row.hidden = !row.hidden;
            button.setAttribute('aria-expanded', String(!row.hidden));
            if (!row.hidden) row.querySelector('select').focus();
        }
        const cancel = event.target.closest('.personel-devir-cancel');
        if (cancel && !busy) {
            const form = cancel.closest('form'); reset(form); form.reset();
            const row = form.closest('tr'); row.hidden = true;
            const opener = document.querySelector(`.personel-devir-open[data-role="${form.dataset.role}"]`);
            opener.setAttribute('aria-expanded', 'false');
            opener.focus();
        }
    });
    document.addEventListener('change', event => {
        const form = event.target.closest('.personel-devir-form');
        if (form && !busy) reset(form);
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !busy) document.querySelectorAll('.personel-devir-form').forEach(reset);
    });
    document.addEventListener('submit', async event => {
        const form = event.target;
        if (form.matches('.personel-hazirla-form')) {
            event.preventDefault();
            if (busy || form.dataset.saved || !form.reportValidity()) return;
            const body = new FormData(form), controls = [...form.querySelectorAll('input,button')];
            const disabled = controls.map(control => control.disabled), message = form.querySelector('.personel-devir-message');
            busy = true; controls.forEach(control => control.disabled = true);
            try {
                const response = await fetch(form.action, { method: 'POST', body, credentials: 'same-origin', headers: { Accept: 'application/json' } });
                const result = await response.json().catch(() => ({}));
                if (!response.ok || result.hazirlandi !== true) throw new Error(result.hata || 'İşlem tamamlanamadı; girişleriniz korunuyor.');
                message.textContent = result.bilgi + ' Güncel durumu görmek için sayfayı yenileyin.';
                form.dataset.saved = 'true';
            } catch (error) { message.textContent = error.message; }
            finally { busy = false; if (!form.dataset.saved) controls.forEach((control,index) => control.disabled = disabled[index]); }
            return;
        }
        if (!form.matches('.personel-devir-form')) return;
        event.preventDefault();
        if (busy || form.dataset.saved || !form.reportValidity()) return;
        const message = form.querySelector('.personel-devir-message');
        const old = form.elements.namedItem('EskiKullaniciId');
        const next = form.elements.namedItem('YeniKullaniciId');
        if (old.value === next.value) { message.textContent = 'İki farklı görevli seçin.'; return; }
        const signature = old.value + '/' + next.value;
        if (form.dataset.confirm !== signature) {
            form.dataset.confirm = signature;
            message.textContent = `${old.selectedOptions[0].textContent} hesabı kapatılacak; görev ${next.selectedOptions[0].textContent} kişisine devredilecek.`;
            form.querySelector('button[type=submit]').textContent = 'Devri onayla';
            return;
        }
        const body = new FormData(form);
        const controls = [...form.querySelectorAll('input,select,button')];
        const disabled = controls.map(control => control.disabled);
        busy = true; controls.forEach(control => control.disabled = true);
        try {
            const response = await fetch(form.action, { method: 'POST', body, credentials: 'same-origin', headers: { Accept: 'application/json' } });
            const result = await response.json().catch(() => ({}));
            if (!response.ok || result.kaydedildi !== true) throw new Error(result.hata || 'Devir tamamlanamadı; güncel durumu kontrol edin.');
            form.dataset.saved = 'true';
            message.textContent = 'Görev devredildi. Güncel personel listesini görmek için sayfayı yenileyin.';
            // Kör tekrar ve diğer formun taslaklarını kaybettiren otomatik sayfa yenilemesi yok.
            document.querySelectorAll('.personel-devir-form input,.personel-devir-form select,.personel-devir-form button,.personel-devir-open')
                .forEach(control => control.disabled = true);
        } catch (error) {
            reset(form); message.textContent = error.message;
        } finally {
            busy = false;
            if (!form.dataset.saved) controls.forEach((control, index) => control.disabled = disabled[index]);
        }
    });
})();
