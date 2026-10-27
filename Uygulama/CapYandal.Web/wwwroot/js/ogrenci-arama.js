"use strict";
(() => {
    const form = document.querySelector('.student-filters');
    const table = document.querySelector('.student-programs table');
    if (!form || !table) return;
    const submit = form.querySelector('[type=submit]');
    if (submit) submit.hidden = true;
    const status = document.createElement('p'); status.setAttribute('role', 'status'); form.after(status);
    let timer, request, generation = 0;
    async function search() {
        if (!form.reportValidity()) return;
        const version = ++generation; request?.abort(); request = new AbortController();
        const url = new URL(form.action, location.href);
        new FormData(form).forEach((value, key) => url.searchParams.set(key, value));
        table.setAttribute('aria-busy', 'true'); status.textContent = '';
        try {
            const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store', signal: request.signal });
            if (!response.ok || response.redirected) throw new Error();
            const document = new DOMParser().parseFromString(await response.text(), 'text/html');
            const updated = document.querySelector('.student-programs table tbody');
            if (!updated) throw new Error();
            if (version !== generation) return;
            table.tBodies[0].replaceWith(updated); history.replaceState(null, '', url);
        } catch (e) {
            if (e.name !== 'AbortError' && version === generation) { status.textContent = 'Arama sonucu alınamadı. Mevcut liste değiştirilmedi; Ara ile yeniden deneyebilirsiniz.'; if (submit) submit.hidden = false; }
        } finally { if (version === generation) table.removeAttribute('aria-busy'); }
    }
    form.addEventListener('submit', event => { event.preventDefault(); clearTimeout(timer); void search(); });
    form.querySelector('input[type=search]')?.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(search, 250); });
    form.querySelectorAll('select').forEach(select => select.addEventListener('change', () => { clearTimeout(timer); void search(); }));
    table.addEventListener('click', event => {
        const row = event.target.closest('.student-detail-row');
        if (row && !event.target.closest('a,button,input,select,textarea')) row.querySelector('a')?.click();
    });
})();
