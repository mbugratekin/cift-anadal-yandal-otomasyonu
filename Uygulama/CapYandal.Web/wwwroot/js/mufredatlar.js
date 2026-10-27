(() => {
    let busy = false, savedButUnread = false;
    const section = () => document.querySelector('.curriculum-section');
    function updateSource() {
        const form = section()?.querySelector('.curriculum-create');
        if (!form) return;
        const source = form.elements.namedItem('KaynakAnaDal');
        source.disabled = form.elements.namedItem('Tur').value === '2';
        source.required = !source.disabled;
        source.closest('label').hidden = source.disabled;
    }
    document.addEventListener('change', event => {
        if (event.target.matches('.curriculum-create select[name=Tur]')) updateSource();
    });
    document.addEventListener('click', event => {
        const button = event.target.closest('.curriculum-open');
        if (!button) return;
        const form = document.getElementById('curriculum-create-form');
        button.closest('.curriculum-heading').after(form);
        form.hidden = !form.hidden; button.setAttribute('aria-expanded', String(!form.hidden));
        if (!form.hidden) form.querySelector('select').focus();
    });
    updateSource();
    document.addEventListener('submit', async event => {
        const form = event.target;
        if (!form.matches('.curriculum-form')) return;
        event.preventDefault();
        if (busy || savedButUnread || !form.reportValidity()) return;
        let message = document.getElementById('curriculum-message');
        const forms = [...section().querySelectorAll('.curriculum-form')];
        if (forms.some(other => other !== form && [...other.querySelectorAll('input:not([type=hidden])')].some(input => input.value !== input.defaultValue))) {
            message.textContent = 'Önce diğer müfredat girişlerini tamamlayın veya temizleyin.';
            message.classList.add('is-error');
            return;
        }
        const body = new FormData(form);
        const oldSection = section();
        const controls = [...oldSection.querySelectorAll('input,select,button')];
        const disabled = controls.map(control => control.disabled);
        busy = true;
        controls.forEach(control => control.disabled = true);
        try {
            const response = await fetch(form.action, { method: 'POST', body, credentials: 'same-origin', headers: { Accept: 'application/json' } });
            const result = await response.json();
            if (!response.ok || result.kaydedildi !== true) throw new Error(result.hata || 'Kaydedilemedi; girişiniz korunuyor.');
            savedButUnread = true;
            const refreshed = await fetch(location.href, { credentials: 'same-origin', cache: 'no-store' });
            if (!refreshed.ok || refreshed.redirected) throw new Error('Kaydedildi; liste okunamadı. Sayfayı yenileyin, işlemi tekrar göndermeyin.');
            const page = new DOMParser().parseFromString(await refreshed.text(), 'text/html');
            const next = page.querySelector('.curriculum-section');
            if (!next?.querySelector('.curriculum-form')) throw new Error('Kaydedildi; sayfayı yenileyin, işlemi tekrar göndermeyin.');
            section().replaceWith(next);
            updateSource();
            savedButUnread = false;
            message = document.getElementById('curriculum-message');
            message.classList.remove('is-error');
            message.textContent = 'Müfredat kaydedildi.';
        } catch (error) {
            message.textContent = error.message;
            message.classList.add('is-error');
        } finally {
            busy = false;
            if (section() === oldSection) controls.forEach((control,index) => control.disabled = savedButUnread || disabled[index]);
        }
    });
})();
