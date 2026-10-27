(() => {
    const area = document.querySelector('.note-live');
    if (!area) return;
    document.addEventListener('keydown', event => {
        if (event.key === 'Tab') area.classList.add('note-keyboard');
    }, true);
    document.addEventListener('pointerdown', () => area.classList.remove('note-keyboard'), true);
    const form = area.querySelector('.note-request');
    const list = area.querySelector('.program-note-list');
    const drafts = area.querySelector('.note-drafts');
    const error = area.querySelector('.note-error');
    const announcement = area.querySelector('.note-announcement');
    const dateFormatter = new Intl.DateTimeFormat('tr-TR', {timeZone: 'Europe/Istanbul', day: '2-digit', month: '2-digit', year: 'numeric'});

    async function request(url, fields) {
        const body = new FormData(form);
        for (const [key, value] of Object.entries(fields)) body.set(key, value);
        const response = await fetch(url, {method: 'POST', body, credentials: 'same-origin', headers: {'Accept': 'application/json'}});
        if (!response.headers.get('Content-Type')?.includes('application/json'))
            throw new Error('Oturum veya bağlantı sorunu. Yazınızı koruyup sayfayı yenileyin.');
        const result = await response.json();
        if (!response.ok) throw new Error(result.hata || 'Not işlemi tamamlanamadı. Tekrar deneyin.');
        return result.not;
    }

    function report(problem) {
        error.textContent = problem instanceof Error ? problem.message : 'Not kaydedilemedi. Tekrar deneyin.';
    }

    function grow(input) {
        input.style.resize = 'none'; input.style.overflow = 'hidden';
        const fit = () => { input.style.height = 'auto'; input.style.height = `${input.scrollHeight}px`; };
        input.addEventListener('input', fit); fit();
    }

    function attachEntry(entry) {
        const input = entry.querySelector('.note-content');
        const remove = entry.querySelector('.note-remove');
        grow(input);
        const editArea = document.createElement('div'); editArea.hidden = true;
        const editLabel = document.createElement('label');
        const editReason = document.createElement('textarea'); editReason.rows = 1; editReason.maxLength = 2000;
        editReason.placeholder = 'Değişiklik gerekçesi'; editReason.setAttribute('aria-label', 'Değişiklik gerekçesi');
        editLabel.append(editReason);
        const save = document.createElement('button'); save.type = 'button'; save.className = 'text-button'; save.textContent = 'Değişikliği kaydet';
        const revert = document.createElement('button'); revert.type = 'button'; revert.className = 'text-button'; revert.textContent = 'Vazgeç';
        editArea.append(editLabel, save, revert); entry.append(editArea);
        const cancelArea = document.createElement('div'); cancelArea.hidden = true;
        const label = document.createElement('label');
        const reason = document.createElement('textarea'); reason.rows = 1; reason.maxLength = 2000;
        reason.placeholder = 'İptal gerekçesi'; reason.setAttribute('aria-label', 'İptal gerekçesi');
        label.append(reason);
        const confirm = document.createElement('button'); confirm.type = 'button'; confirm.className = 'text-button'; confirm.textContent = 'Notu iptal et';
        const dismiss = document.createElement('button'); dismiss.type = 'button'; dismiss.className = 'text-button'; dismiss.textContent = 'Vazgeç';
        cancelArea.append(label, confirm, dismiss); entry.append(cancelArea);
        dismiss.addEventListener('click', () => { if (!deleting) { cancelArea.hidden = true; remove.focus(); } });
        let savedText = input.value;
        let pendingSave = null;
        let deleting = false;
        revert.addEventListener('click', () => {
            if (pendingSave || deleting) return;
            input.value = savedText; input.dispatchEvent(new Event('input'));
            editReason.value = ''; editArea.hidden = true; input.focus();
        });
        remove.addEventListener('pointerdown', event => event.preventDefault());
        input.addEventListener('blur', () => {
            if (input.value !== savedText) { editArea.hidden = false; grow(editReason); }
        });
        save.addEventListener('click', () => {
            if (pendingSave || deleting || input.value === savedText) return;
            if (!input.value.trim() || !editReason.value.trim()) { error.textContent = 'Not metnini ve değişiklik gerekçesini yazın.'; return; }
            input.readOnly = true;
            pendingSave = (async () => {
                try {
                    const note = await request(form.dataset.editUrl, {NotId: entry.dataset.id, Surum: entry.dataset.version, Metin: input.value, Gerekce: editReason.value});
                    savedText = note.metin;
                    input.value = savedText;
                    entry.dataset.version = note.surum;
                    editArea.hidden = true; editReason.value = '';
                    error.textContent = '';
                    announcement.textContent = 'Not kaydedildi.';
                    return true;
                } catch (problem) { report(problem); return false; }
                finally { input.readOnly = false; }
            })();
            pendingSave.finally(() => { pendingSave = null; });
        });
        remove.addEventListener('click', () => { cancelArea.hidden = false; grow(reason); reason.focus(); });
        confirm.addEventListener('click', async () => {
            if (deleting) return;
            if (!reason.value.trim()) { error.textContent = 'İptal gerekçesini yazın.'; reason.focus(); return; }
            deleting = true;
            remove.setAttribute('aria-disabled', 'true');
            try {
                if (pendingSave && !await pendingSave) return;
                input.readOnly = true;
                await request(form.dataset.deleteUrl, {NotId: entry.dataset.id, Surum: entry.dataset.version, Gerekce: reason.value});
                const focusTarget = entry.nextElementSibling?.querySelector('textarea')
                    || entry.previousElementSibling?.querySelector('textarea');
                entry.remove();
                ensureBlank();
                (focusTarget || drafts.querySelector('textarea')).focus();
                error.textContent = '';
                announcement.textContent = 'Not iptal edildi; metin ve işlem geçmişi korundu.';
            } catch (problem) { report(problem); }
            finally { deleting = false; input.readOnly = false; remove.removeAttribute('aria-disabled'); }
        });
    }

    function renderEntry(note) {
        const entry = document.createElement('li');
        entry.className = 'program-note-entry';
        entry.dataset.id = note.id;
        entry.dataset.version = note.surum;
        const heading = document.createElement('div');
        heading.className = 'note-heading';
        const time = document.createElement('time');
        time.dateTime = note.olusturmaAni;
        time.textContent = dateFormatter.format(new Date(note.olusturmaAni));
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'note-remove';
        remove.textContent = '−';
        remove.title = 'Notu iptal et';
        remove.setAttribute('aria-label', `${time.textContent} tarihli notu iptal et`);
        const input = document.createElement('textarea');
        input.className = 'note-content';
        input.rows = 1;
        input.maxLength = 2000;
        input.setAttribute('aria-label', `${time.textContent} tarihli not`);
        input.value = note.metin;
        heading.append(time, remove);
        entry.append(heading, input);
        list.append(entry);
        attachEntry(entry);
    }

    function ensureBlank() {
        if (!drafts.lastElementChild || drafts.lastElementChild.value.trim()) addDraft();
    }

    function attachDraft(input) {
        grow(input);
        let saving = false;
        input.addEventListener('blur', async () => {
            if (saving) return;
            if (!input.value.trim()) {
                if (drafts.children.length > 1) input.remove();
                ensureBlank();
                return;
            }
            saving = true;
            input.disabled = true;
            try {
                const note = await request(form.action, {Metin: input.value});
                renderEntry(note);
                input.remove();
                error.textContent = '';
                announcement.textContent = 'Not kaydedildi.';
            } catch (problem) { report(problem); }
            finally { saving = false; input.disabled = false; ensureBlank(); }
        });
    }

    function addDraft() {
        const input = document.createElement('textarea');
        input.className = 'program-note-input';
        input.rows = 1;
        input.maxLength = 2000;
        input.setAttribute('aria-label', 'Yeni program takip notu');
        drafts.append(input);
        attachDraft(input);
    }
    for (const entry of list.children) attachEntry(entry);
    attachDraft(drafts.querySelector('textarea'));
})();
