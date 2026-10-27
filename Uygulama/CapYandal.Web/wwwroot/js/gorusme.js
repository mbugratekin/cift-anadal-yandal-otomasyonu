"use strict";
document.querySelector('.meeting-edit')?.addEventListener('click', event => {
    const editor = document.getElementById('meeting-editor');
    editor.hidden = !editor.hidden;
    event.currentTarget.setAttribute('aria-expanded', String(!editor.hidden));
    if (!editor.hidden) editor.querySelector('textarea').focus();
});
// Tek görüşme alanı — metin mevcut blok/oda ve gün-saat sözleşmesine çevrilir; eksik satır kaydedilmez.
function gorusmeMetniniCoz(text) {
    const lines = text.trim().split(/\r?\n/).map(x => x.trim()).filter(Boolean);
    if (!lines.length) return { blok: "", oda: "", araliklar: [] };
    const onlySchedule = /^(pazartesi|salı|çarşamba|perşembe|cuma|cumartesi|pazar)\s*:?\s*\d/u.test(lines[0].toLocaleLowerCase("tr-TR"));
    const first = onlySchedule ? "" : lines.shift(), location = first.match(/^(.*?)\s*(?:[·,|–-]\s*)?\bOda(?:\s*:\s*|\s+)(.+)$/iu);
    const blok = location ? location[1].replace(/[·,|–\s-]+$/u, "").trim() : first;
    const oda = location ? location[2].trim() : "";
    if (blok.length > 80 || oda.length > 80 || lines.length > 32) throw new Error("Konum en fazla 80, gün–saat satırları en fazla 32 olabilir.");
    const days = ["pazartesi", "salı", "çarşamba", "perşembe", "cuma", "cumartesi", "pazar"];
    const araliklar = lines.map(line => {
        const match = line.toLocaleLowerCase("tr-TR").match(/^(pazartesi|salı|çarşamba|perşembe|cuma|cumartesi|pazar)\s*:?\s*(\d{1,2})(?:[.:](\d{2}))?\s*[–—-]\s*(\d{1,2})(?:[.:](\d{2}))?$/u);
        if (!match) throw new Error("Gün ve saat aralığını Salı 14.00–16.30 biçiminde yazın.");
        const time = (h, m = "00") => {
            if (Number(h) > 23 || Number(m) > 59) throw new Error("Geçerli bir saat aralığı yazın.");
            return h.padStart(2, "0") + ":" + m;
        };
        const baslangic = time(match[2], match[3]), bitis = time(match[4], match[5]);
        if (baslangic >= bitis) throw new Error("Bitiş saati başlangıçtan sonra olmalı.");
        return { gun: days.indexOf(match[1].toLocaleLowerCase("tr-TR")) + 1, baslangic, bitis };
    });
    if (new Set(araliklar.map(x => JSON.stringify(x))).size !== araliklar.length) throw new Error("Aynı gün ve saat satırı tekrar ediyor.");
    return { blok, oda, araliklar };
}

// Otomatik kayıt — alan terk edilince CSRF/rowversion ile kaydeder; belirsiz sonuç otomatik tekrarlanmaz.
document.querySelectorAll('.meeting-form').forEach(form => {
    const field = form.querySelector('textarea'), message = form.querySelector('.meeting-message');
    const locationField=form.querySelector('[name=GorusmeKonumu]');
    const emailField=form.querySelector('[name=IletisimEpostasi]');
    let savedEmail=emailField?.value;
    const textValue=()=>locationField ? [locationField.value.trim(),field.value.trim()].filter(Boolean).join('\n') : field.value.trim();
    const actions = form.querySelector('.meeting-actions');
    let saved = textValue(), savedHours=field.value, savedLocation=locationField?.value, busy = false, uncertain = false;
    function fit() {
        if (form.classList.contains('staff-meeting-strip')) { field.style.height='5rem'; return; }
        field.style.height = 'auto';
        field.style.height = Math.max(80, field.scrollHeight + 2) + 'px';
    }
    const changed=()=>{fit();actions.hidden=textValue()===saved && emailField?.value===savedEmail;message.textContent='';};
    emailField?.addEventListener('input',changed);
    fit();field.addEventListener('input',changed);locationField?.addEventListener('input',changed);
    form.querySelector('.meeting-cancel')?.addEventListener('click', () => { if(busy || uncertain) return; field.value=savedHours;if(locationField)locationField.value=savedLocation;if(emailField)emailField.value=savedEmail; actions.hidden=true; message.textContent=''; fit(); });
    async function save() {
        if (busy || uncertain || (textValue() === saved && emailField?.value===savedEmail)) return;
        if (emailField && !emailField.checkValidity()) { emailField.reportValidity(); return; }
        let body;
        try { body = { ...gorusmeMetniniCoz(textValue()), surum: form.dataset.version || null }; }
        catch (error) { message.textContent = error.message; return; }
        if(emailField)body.iletisimEpostasi=emailField.value.trim();
        const submitted = textValue(); busy = true; field.readOnly = true;if(locationField)locationField.readOnly=true; message.textContent = 'Kaydediliyor…';
        if(emailField)emailField.readOnly=true;
        try {
            const response = await fetch(form.action, { method: 'POST', credentials: 'same-origin', headers: {
                'Content-Type': 'application/json', 'RequestVerificationToken': form.querySelector('[name=__RequestVerificationToken]').value }, body: JSON.stringify(body) });
            if (response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error();
            const result = await response.json();
            if (response.ok && result.kaydedildi && result.surum) {
                saved = submitted;savedHours=field.value;savedLocation=locationField?.value;savedEmail=emailField?.value; form.dataset.version = result.surum; message.textContent = ''; actions.hidden = true;
                const strip = form.closest('.chat-own-meeting');
                if (strip?.querySelector('[data-meeting-location]') && strip.querySelector('[data-meeting-hours]')) {
                    strip.querySelector('[data-meeting-location]').textContent = [body.blok, body.oda ? 'Oda ' + body.oda : ''].filter(Boolean).join(' - ');
                    const hours = strip.querySelector('[data-meeting-hours]');
                    const days = ['', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi', 'Pazar'];
                    hours.replaceChildren(...body.araliklar.map(x => {
                        const line = document.createElement('div'); line.textContent = `${days[x.gun]} ${x.baslangic.replace(':', '.')}–${x.bitis.replace(':', '.')}`; return line;
                    }));
                    if (!body.araliklar.length) hours.textContent = 'Henüz belirtilmedi.';
                }
            } else {
                uncertain = response.ok || response.status >= 500 || response.status === 409 || response.status === 403;
                message.textContent = result.hata || 'Kaydedilemedi. Alanı kontrol edin.';
            }
        } catch { uncertain = true; message.textContent = 'Kayıt sonucu doğrulanamadı. Yeniden göndermeden sayfayı yenileyin.'; }
        finally { busy = false; field.readOnly = uncertain;if(locationField)locationField.readOnly=uncertain;if(emailField)emailField.readOnly=uncertain; }
    }
    form.addEventListener('submit', event => { event.preventDefault(); save(); });
    window.addEventListener('beforeunload', event => { if (busy || uncertain || textValue() !== saved || emailField?.value!==savedEmail) { event.preventDefault(); event.returnValue = ''; } });
});
