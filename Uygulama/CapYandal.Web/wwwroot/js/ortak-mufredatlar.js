"use strict";
(() => {
    const root = document.querySelector('.common-curricula'); if (!root) return;
    const create = root.querySelector('.common-curriculum-create'), opener = root.querySelector('.common-curriculum-open');
    opener?.addEventListener('click', () => { create.hidden = !create.hidden; opener.setAttribute('aria-expanded', String(!create.hidden)); if (!create.hidden) create.querySelector('input').focus(); });
    root.querySelector('.common-curriculum-cancel')?.addEventListener('click', () => { create.reset(); create.hidden = true; opener.setAttribute('aria-expanded','false'); });
    root.querySelector('.common-show-passive').addEventListener('change', event => {
        document.querySelectorAll('[data-curriculum-active="false"]').forEach(row => { row.hidden = !event.target.checked; });
    });
    let busy = false, uncertain = false;
    root.addEventListener('submit', async event => {
        const form = event.target; if (!form.matches('.common-curriculum-form')) return; event.preventDefault();
        if (busy || uncertain || !form.reportValidity()) return;
        const message = root.querySelector('.common-curriculum-message');
        const unsaved = [...document.querySelectorAll('.template-form')].some(f => f.dataset.busy || f.querySelector('[data-new]') || [...f.elements].filter(x=>x.matches('input,select')).some(x=>x.value !== (x instanceof HTMLSelectElement ? [...x.options].find(o=>o.defaultSelected)?.value ?? x.options[0]?.value : x.defaultValue)));
        if (unsaved) { message.textContent = 'Önce ders listesindeki değişiklikleri kaydedin veya Vazgeç ile geri alın.'; return; }
        const data = new FormData(form), buttons = [...root.querySelectorAll('button')]; busy = true; buttons.forEach(b=>b.disabled=true); message.textContent='';
        try { const response = await fetch(form.action,{method:'POST',body:data,credentials:'same-origin'});
            if(response.redirected) throw Error('Oturumu kontrol edin.'); const result = await response.json();
            if(!response.ok || !result.kaydedildi) { uncertain = response.status>=500; throw Error(result.hata || 'Kayıt tamamlanamadı.'); }
            uncertain = true; location.reload();
        } catch(e) { if(e instanceof TypeError || e instanceof SyntaxError) uncertain=true; message.textContent=uncertain?'Sonuç doğrulanamadı. Tekrar göndermeden sayfayı yenileyin.':e.message; }
        finally { busy=false; buttons.forEach(b=>b.disabled=uncertain); }
    });
})();
