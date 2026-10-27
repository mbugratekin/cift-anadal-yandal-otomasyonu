"use strict";
(() => {
  const root = document.getElementById("academic-rules");
  if (!root) return;
  const form = document.getElementById("academic-rules-form"), body = document.getElementById("academic-rules-body");
  const message = document.getElementById("academic-rules-message"), tabs = [...root.querySelectorAll(".rule-tabs button[data-program]")];
  const writable = root.dataset.write === "true", states = new Map();
  const numericBody = document.getElementById("academic-numeric-body");
  const numericNames = [[1, "Onaylı tamamlanan ders sayısı"], [2, "Onaylı tamamlanan kredi"], [3, "Onaylı tamamlanan AKTS"],
    [4, "Kabulden sonra geçen takvim dönemi (pasif dönemler dâhil)"], [6, "Ardışık onaylı ders kaydı görünmeyen dönem"],
    [7, "Atanmış ders havuzunun AKTS toplamı"], [8, "Onaylı kişisel listedeki ders sayısı"], [9, "Onaylı kişisel listenin AKTS toplamı"]];
  const signature = value => JSON.stringify(value, (key, item) => ['normalEsik','altEsik','donemSayisi','altSinir','ustSinir'].includes(key) && item !== null && item !== '' ? Number(String(item).replace(',', '.')) : item);
  const numericDirty = s => signature(s?.numericDraft ?? []) !== signature(s?.numericSaved ?? []);
  const controls = [[1, "En az not ortalaması"], [2, "Not ortalamasında düşme hakkı"], [3, "Üst üste düşük not ortalaması"]];
  let program = 1, busy = false, removal = null, editing = null;
  const copy = value => structuredClone(value);
  const current = () => states.get(program);
  const safeError = e => e instanceof SyntaxError ? "Yanıt doğrulanamadı. Tekrar göndermeden sayfayı yenileyin."
    : e instanceof TypeError ? "Bağlantı kesildi; otomatik tekrar yapılmadı. Sayfayı yenileyin." : e.message;
  const dirty = () => current() && (signature(current().draft) !== signature(current().saved) || numericDirty(current()));
  const decimal = value => value == null ? "" : Number(value).toFixed(2).replace(".", ",");
  function refreshSummary() {
    if(!current()) return;
    root.querySelectorAll('[data-summary-key]').forEach(row=>{
      const [group,index]=row.dataset.summaryKey.split(':').map(Number);
      if(group===0) {
        const r=current().draft[index]; if(!r) return;
        let text=`En az ${r.normalEsik || '—'}`;
        if(r.kontrolTuru===2) text+=` | ${r.donemSayisi ?? '—'} düşük dönem hakkı`;
        if(r.kontrolTuru===3) text+=` | ${r.donemSayisi ?? '—'} ardışık dönem`;
        if(r.kontrolTuru===2 && r.altEsik) text+=` | Alt sınır ${r.altEsik}`;
        row.cells[2].textContent=text;
      } else {
        const r=current().numericDraft[index];if(!r)return;
        row.cells[0].textContent=numericNames.find(([id])=>id===Number(r.olcut))?.[1] || 'Ölçüt seçin';
        row.cells[1].textContent=[...document.querySelectorAll('#numeric-curricula option')].find(o=>o.value===r.mufredatId)?.textContent || 'Seçili programın tüm müfredatları';
        row.cells[2].textContent=[r.altSinir!==''&&r.altSinir!=null?`En az ${r.altSinir}`:'',r.ustSinir!==''&&r.ustSinir!=null?`En fazla ${r.ustSinir}`:''].filter(Boolean).join(' | ') || '—';
      }
    });
  }
  function say(text, error = false) { message.textContent = text; message.classList.toggle("is-error", error); }
  function node(tag, text, className) { const el = document.createElement(tag); if (text != null) el.textContent = text; if (className) el.className = className; return el; }
  function statusButtons() {
    tabs.forEach(tab => { const id = Number(tab.dataset.program), s = states.get(id);
      tab.disabled = busy; tab.setAttribute("aria-pressed", String(id === program));
      tab.textContent = id === 1 ? "ÇAP" : "Yan Dal"; });
    root.querySelectorAll(".rules-toolbar button,.rule-local-actions button").forEach(button => {
      button.disabled = busy || !writable || !current() || !!current().error || (button.type === "submit" || button.classList.contains("rule-save")) && !dirty();
    });
  }
  function editableSelect(options, value, field, index, name) {
    const el = node("select"); el.dataset.field = field; el.dataset.index = String(index); el.setAttribute("aria-label", name);
    const blank = new Option("", ""); blank.hidden = true; el.add(blank);
    options.forEach(([key, text]) => {
      const option = new Option(text, String(key));
      if (field === "definition") option.disabled = current().draft.some((r, i) => i !== index && `${r.kapsam}:${r.amac}` === String(key));
      el.add(option);
    }); el.value = value == null ? "" : String(value);
    el.disabled = !writable || busy || !!current()?.error; return el;
  }
  function numberInput(rule, field, index, name, applicable) {
    if (!applicable) { const cell = node('div', name, 'rule-parameter'); cell.append(node('span', '—')); return cell; }
    const cell = node("label", name, "rule-parameter");
    const input = node("input"); input.type = "text"; input.inputMode = field === "donemSayisi" ? "numeric" : "decimal";
    input.value = rule[field] ?? ""; input.dataset.field = field; input.dataset.index = String(index); input.setAttribute("aria-label", `${index + 1}. kural ${name}`);
    input.maxLength = field === "donemSayisi" ? 10 : 8; input.disabled = !writable || busy || !!current()?.error; cell.append(input); return cell;
  }
  function render() {
    body.replaceChildren(); const state = current();
    renderNumeric(state);
    if (!state || state.error && !state.draft.length) { const row = node("tr"), cell = node("td", state?.error ? "Koşullar okunamadı." : "Kurallar yükleniyor."); cell.colSpan = 4; row.append(cell); body.append(row); statusButtons(); return; }
    const rules = state.draft, scope = program === 1 ? 2 : 3;
    if (!rules.length && !state.numericDraft?.length) { const row = node("tr"), cell = node("td", "Koşul yok."); cell.colSpan = 4; row.append(cell); body.append(row); }
    rules.forEach((rule, index) => {
      const row = node("tr", null, "rule-row"); row.dataset.index = String(index); row.append(node("td", String(index + 1)), node("td", "GANO"));
      const definition = node("td"), options = [["1:1", "Ana Dal — Devam"], ["1:2", "Ana Dal — Tamamlama"],
        [`${scope}:1`, `${program === 1 ? "ÇAP" : "Yan Dal"} — Devam`], [`${scope}:2`, `${program === 1 ? "ÇAP" : "Yan Dal"} — Tamamlama`]];
      definition.append(editableSelect(options, rule.kapsam && rule.amac ? `${rule.kapsam}:${rule.amac}` : null, "definition", index, `${index + 1}. kural kapsamı ve amacı`)); row.append(definition);
      const control = node("td"); control.append(editableSelect(controls, rule.kontrolTuru, "kontrolTuru", index, `${index + 1}. kontrol türü`));
      const parameters = node("div", null, "rule-parameters");
      parameters.append(numberInput(rule, "normalEsik", index, "GANO sınırı", true), numberInput(rule, "altEsik", index, "Alt sınır (varsa)", rule.kontrolTuru === 2),
        numberInput(rule, "donemSayisi", index, rule.kontrolTuru === 2 ? "Düşük dönem hakkı" : "Dönem sayısı", rule.kontrolTuru === 2 || rule.kontrolTuru === 3));
      control.append(parameters); row.append(control);
      const action = node("td", null, "rule-operation");
      if (writable) { const button = node("button", removal === index ? "Onayla" : "Kaldır", "rule-remove"); button.type = "button"; button.dataset.remove = String(index); button.disabled = busy || !!state.error; button.setAttribute("aria-label", `${index + 1}. kuralı ${removal === index ? "kaldırmayı onayla" : "kaldır"}`); action.append(button); }
      else action.append(node("span", "—"));
      row.append(action); body.append(row);
      const description = node("tr", null, "description-row"), cell = node("td"); cell.colSpan = 5;
      const text = node("textarea"); text.value = rule.aciklama ?? ""; text.rows = 1; text.maxLength = 2000; text.placeholder = "Kural açıklaması";
      text.addEventListener("input", () => fitDescription(text));
      text.dataset.field = "aciklama"; text.dataset.index = String(index); text.setAttribute("aria-label", `${index + 1}. kural açıklaması`); text.disabled = !writable || busy || !!state.error;
      const descriptionLabel = node("label", "Koşulun açıklaması"); descriptionLabel.append(text); cell.append(descriptionLabel); description.append(cell); body.append(description);
      fitDescription(text);
    });
    compactRows(); statusButtons();
  }
  function compactRows() {
    const editor = document.getElementById('academic-rule-editor');
    editor.replaceChildren(); editor.hidden = true;
    [body, numericBody].filter(Boolean).forEach((container, group) => {
      [...container.querySelectorAll(':scope > .rule-row')].forEach((row, index) => {
        const key = `${group}:${index}`, detail = row.nextElementSibling;
        const cells = [...row.cells], summary = node('tr', null, 'rule-summary');
        let title, scope, value;
        if (group === 0) {
          const r = current().draft[index];
          title = `${r.kapsam === 1 ? 'Ana dal' : program === 1 ? 'ÇAP' : 'Yan Dal'} GANO ${r.amac === 2 ? 'tamamlama' : 'devam'} koşulu`;
          scope = program === 1 ? 'ÇAP' : 'Yan Dal';
          value = `En az ${r.normalEsik || '—'}`;
          if (r.kontrolTuru === 2) value += ` | ${r.donemSayisi ?? '—'} düşük dönem hakkı`;
          if (r.kontrolTuru === 3) value += ` | ${r.donemSayisi ?? '—'} ardışık dönem`;
          if (r.kontrolTuru === 2 && r.altEsik != null && r.altEsik !== '') value += ` | Alt sınır ${r.altEsik}`;
        } else {
          const r = current().numericDraft[index];
          title = numericNames.find(([id]) => id === Number(r.olcut))?.[1] ?? 'Eski ölçüt (değerlendirilmez)';
          scope = cells[2].querySelector('select')?.selectedOptions[0]?.textContent || 'Tüm müfredatlar';
          value = [r.altSinir !== '' && r.altSinir != null ? `En az ${r.altSinir}` : '', r.ustSinir !== '' && r.ustSinir != null ? `En fazla ${r.ustSinir}` : ''].filter(Boolean).join(' | ') || '—';
        }
        summary.dataset.summaryKey = key;
        summary.append(node('td', title), node('td', scope), node('td', value));
        const action = node('td'), button = node('button', editing === key ? 'Kapat' : writable ? 'Düzenle' : 'Ayrıntı', 'rule-edit');
        button.type = 'button'; button.dataset.ruleEdit = key; button.disabled = busy; button.setAttribute('aria-expanded', String(editing === key));
        action.append(button); summary.append(action); row.before(summary);
        const wrapper = node('div', null, 'rule-editor-fields');
        cells.slice(1).forEach((cell, i) => {
          const field = node('div', null, 'rule-editor-field');
          if (group === 0 && i === 0) return;
          if (i < 3) field.append(node('span', (group === 0 ? ['', 'GANO kapsamı ve amacı', 'Değerlendirme yöntemi ve değerler'] : ['Kontrol edilecek değer', 'Uygulanacağı müfredat', 'Gerekli değerler'])[i], 'rule-editor-label'));
          field.append(...cell.childNodes); wrapper.append(field);
        });
        row.replaceChildren(); const editorCell = node('td'); editorCell.colSpan = 4; editorCell.append(wrapper); row.append(editorCell);
        row.hidden = editing !== key; if (detail) { detail.hidden = editing !== key; detail.cells[0].colSpan = 4; }
        if (editing === key) {
          editor.hidden = false;
          const table = node('table', null, 'rules-table unified-rules-table'), editorBody = node('tbody');
          editorBody.append(row); if (detail) editorBody.append(detail);
          table.append(editorBody); editor.append(table);
        }
      });
    });
    const globalActions = root.querySelector('.rule-actions');
    if (globalActions) globalActions.hidden = editing !== null;
    if (editing !== null && writable) {
      const actions = node('div', null, 'rule-local-actions');
      const save = node('button', 'Kaydet', 'rule-save'); save.type = 'submit';
      const cancel = node('button', 'Vazgeç', 'rule-reset'); cancel.type = 'button';
      cancel.addEventListener('click', () => document.getElementById('academic-reset').click());
      actions.append(save,cancel); editor.append(actions);
    }
    fitDescriptions();
  }
  root.addEventListener('click', event => {
    const button = event.target.closest('[data-rule-edit]'); if (!button || busy) return;
    if (dirty()) { say('Değişikliği kaydedin veya Vazgeç ile geri alın.', true); return; }
    editing = editing === button.dataset.ruleEdit ? null : button.dataset.ruleEdit; render();
  });
  function renderNumeric(state) {
    if (!numericBody) return;
    numericBody.replaceChildren();
    const rows = state?.numericDraft ?? [];
    rows.forEach((r, index) => {
      const row = node("tr", null, "rule-row"); row.append(node("td", String((state.draft?.length ?? 0) + index + 1)));
      function field(tag, key, label) {
        const el = node(tag); el.dataset.numericField = key; el.dataset.numericIndex = String(index); el.setAttribute("aria-label", `${index + 1}. sayısal koşul ${label}`);
        el.disabled = !writable || busy || !!state.error; return el;
      }
      const metric = field("select", "olcut", "ölçüt");
      numericNames.forEach(([id, name]) => metric.add(new Option(name, String(id)))); metric.value = r.olcut ?? "1";
      if (Number(r.olcut) === 5) { metric.add(new Option("Başvuru yarıyılı (eski kayıt, değerlendirilmez)", "5")); metric.value = "5"; }
      const scope = field("select", "mufredatId", "kapsam"); scope.add(new Option("Seçili programın tüm müfredatları", ""));
      document.querySelectorAll("#numeric-curricula option").forEach(option => { if (Number(option.dataset.program) === program) scope.add(new Option(option.textContent, option.value)); });
      scope.value = r.mufredatId ?? "";
      [metric, scope].forEach(el => {
        const cell = node("td"), wrapper = node("div", null, "numeric-choice"), selected = node("span");
        selected.setAttribute("aria-hidden", "true");
        const updateLabel = () => { selected.textContent = el.selectedOptions[0]?.textContent ?? ""; wrapper.title = selected.textContent; };
        el.addEventListener("change", updateLabel); updateLabel();
        wrapper.append(selected, el); cell.append(wrapper); row.append(cell);
      });
      const condition = node("td"), parameters = node("div", null, "rule-parameters");
      ["altSinir", "ustSinir"].forEach(key => { const label = key === "altSinir" ? "En az (varsa)" : "En fazla (varsa)", input = field("input", key, label); input.type = "text"; input.inputMode = "decimal"; input.maxLength = 12; input.value = r[key] ?? ""; const wrapper = node("label", label, "rule-parameter"); wrapper.append(input); parameters.append(wrapper); });
      condition.append(parameters); row.append(condition);
      const text = field("textarea", "dayanak", "kuralın kaynağı"); text.value = r.dayanak ?? ""; text.rows = 1; text.maxLength = 1000; text.placeholder = "Yönerge maddesi / kurul kararı";
      text.style.resize = "none"; text.style.overflow = "hidden";
      text.addEventListener("input", () => fitDescription(text));
      const action = node("td", null, "rule-operation"); if (writable) { const button = node("button", "Kaldır", "rule-remove"); button.type = "button"; button.dataset.numericRemove = String(index); button.disabled = busy || !!state.error; action.append(button); }
      row.append(action); numericBody.append(row);
      const sourceRow = node("tr", null, "numeric-source-row"), source = node("td"), label = node("label", "Kuralın kaynağı");
      source.colSpan = 5; label.append(text); source.append(label); sourceRow.append(source); numericBody.append(sourceRow); fitDescription(text);
    });
  }
  function numericChange(event) {
    const el = event.target; if (!writable || busy || !current() || current().error || !el.dataset.numericField) return;
    const row = current().numericDraft[Number(el.dataset.numericIndex)]; if (!row) return;
    row[el.dataset.numericField] = el.dataset.numericField === "olcut" ? Number(el.value) : el.value;
    statusButtons(); refreshSummary(); say("");
  }
  form.addEventListener("input", numericChange);
  form.addEventListener("change", numericChange);
  form.addEventListener("click", event => { const button = event.target.closest("[data-numeric-remove]"); if (!button || busy || !writable || current()?.error) return; current().numericDraft.splice(Number(button.dataset.numericRemove), 1); editing = null; render(); });
  document.getElementById("academic-numeric-add")?.addEventListener("click", () => { if (busy || !writable || !current() || current().error) return; current().numericDraft.push({ olcut: null, mufredatId: null, altSinir: "", ustSinir: "", dayanak: "" }); render(); });
  function fitDescription(text) { text.style.height = "auto"; text.style.height = `${text.scrollHeight}px`; }
  function fitDescriptions() { body.querySelectorAll("textarea").forEach(fitDescription); }
  window.addEventListener("resize", fitDescriptions);
  document.fonts?.ready.then(fitDescriptions);
  async function read(id, force = false) {
    if (busy) return;
    program = id; removal = null; editing = null;
    if (states.has(id) && !force) { say(current().error ?? "", !!current().error); render(); return; }
    busy = true; render(); say("Kurallar yükleniyor.");
    try {
      const url = new URL(root.dataset.readUrl, window.location.href); url.searchParams.set("program", String(id));
      const response = await fetch(url, { credentials: "same-origin", cache: "no-store" });
      if (!response.ok) throw new Error(response.status === 409 ? "Eski kural sürümü doğrulanamadı; otomatik uyarlama yapılmadı." : "Kurallar okunamadı. Sayfayı yenileyin.");
      const data = await response.json();
      if (data.durum !== 0 || !Array.isArray(data.kurallar)) throw new Error("Kural yanıtı doğrulanamadı.");
      const rows = data.kurallar.map(r => ({ kuralKimligi: r.kuralKimligi, kapsam: r.kapsam, amac: r.amac, kontrolTuru: r.kontrolTuru,
        normalEsik: decimal(r.normalEsik), altEsik: r.altEsik == null ? null : decimal(r.altEsik), donemSayisi: r.donemSayisi == null ? null : String(r.donemSayisi), aciklama: r.aciklama }));
      const numeric = (data.sayisalKurallar ?? []).map(r => ({ ...r, altSinir: r.altSinir == null ? "" : String(r.altSinir).replace(".", ","), ustSinir: r.ustSinir == null ? "" : String(r.ustSinir).replace(".", ",") }));
      states.set(id, { version: data.surumId ?? null, saved: copy(rows), draft: copy(rows), numericSaved: copy(numeric), numericDraft: copy(numeric), error: null }); say("");
    } catch (e) { const text = safeError(e), previous = states.get(id); states.set(id, previous ? { ...previous, error: text } : { version: null, saved: [], draft: [], error: text }); say(text, true); }
    finally { busy = false; render(); }
  }
  function change(event) {
    if (!writable || busy || !event.target.dataset.field || !current() || current().error) return;
    const el = event.target, index = Number(el.dataset.index), rule = current().draft[index]; if (!rule) return;
    const field = el.dataset.field; removal = null;
    if (field === "definition") {
      const [scope, purpose] = el.value.split(":").map(Number);
      if (scope && purpose && current().draft.some((r, i) => i !== index && r.kapsam === scope && r.amac === purpose)) {
        say("Aynı GANO kapsamı ve amaç için ikinci kural eklenemez.", true); render(); return;
      }
      rule.kapsam = scope || null; rule.amac = purpose || null; render();
    }
    else if (field === "kontrolTuru") { rule.kontrolTuru = Number(el.value) || null; rule.altEsik = rule.kontrolTuru === 2 ? "" : null; rule.donemSayisi = rule.kontrolTuru === 2 || rule.kontrolTuru === 3 ? "" : null; render(); }
    else { rule[field] = el.value; el.removeAttribute("aria-invalid"); statusButtons(); refreshSummary(); }
    say("");
  }
  form.addEventListener("input", event => { if (event.target.tagName !== "SELECT") change(event); });
  form.addEventListener("change", event => { if (event.target.tagName === "SELECT") change(event); });
  form.addEventListener("click", event => {
    const button = event.target.closest("[data-remove]"); if (!button || busy || !writable) return;
    const index = Number(button.dataset.remove);
    if (removal === index) { current().draft.splice(index, 1); removal = null; } else removal = index;
    say(""); render(); body.querySelector(`[data-remove="${index}"]`)?.focus();
  });
  document.addEventListener("click", event => {
    if (removal != null && !event.target.closest("[data-remove]")) {
      removal = null;
      body.querySelectorAll("[data-remove]").forEach(button => {
        button.textContent = "Kaldır"; button.setAttribute("aria-label", `${Number(button.dataset.remove) + 1}. kuralı kaldır`);
      });
    }
  });
  document.addEventListener("keydown", event => { if (event.key === "Escape" && removal != null) { removal = null; render(); } });
  document.addEventListener("academic-navigation", event => {
    if (busy || [...states.values()].some(s => signature(s.draft) !== signature(s.saved) || numericDirty(s))) {
      event.preventDefault(); say("Kaydedilmemiş koşulları önce kaydedin veya Vazgeç ile geri alın.", true);
    }
  });
  tabs.forEach(tab => tab.addEventListener("click", () => { if (!busy) void read(Number(tab.dataset.program)); }));
  document.getElementById("academic-add")?.addEventListener("click", () => {
    if (busy || !writable || !current() || current().error) return;
    if (dirty()) { say('Önce açık koşulu kaydedin veya Vazgeç ile geri alın.', true); return; }
    if (document.getElementById('academic-add-kind')?.value === 'numeric') {
      current().numericDraft.push({ olcut: 1, mufredatId: null, altSinir: "", ustSinir: "", dayanak: "" });
      editing = `1:${current().numericDraft.length - 1}`; say(''); render(); numericBody.lastElementChild?.previousElementSibling?.querySelector('select')?.focus(); return;
    }
    current().draft.push({ kuralKimligi: null, kapsam: null, amac: null, kontrolTuru: null, normalEsik: "", altEsik: null, donemSayisi: null, aciklama: "" }); removal = null; say(""); render();
    editing = `0:${current().draft.length - 1}`; render(); body.lastElementChild?.previousElementSibling?.querySelector("select")?.focus();
  });
  document.getElementById("academic-reset")?.addEventListener("click", () => { if (busy || !current()) return; current().draft = copy(current().saved); current().numericDraft = copy(current().numericSaved); removal = null; editing = null; say(""); render(); });
  form.addEventListener("submit", async event => {
    event.preventDefault(); if (busy || !writable || !current() || current().error || !dirty()) return;
    const id = program, state = current(); let rows, numericRows;
    try {
      const parse = text => { if (!/^[0-4]([.,][0-9]{1,2})?$/.test(String(text)) || Number(String(text).replace(",", ".")) > 4) throw new Error("GANO değerlerini 0–4 arasında, en fazla iki ondalık girin."); return Number(String(text).replace(",", ".")); };
      const definitions = new Set();
      rows = state.draft.map((r, i) => {
        if (!r.kapsam || !r.amac || !r.kontrolTuru) throw new Error(`${i + 1}. kuralın kapsamını ve kontrol türünü seçin.`);
        const definition = `${r.kapsam}:${r.amac}`;
        if (definitions.has(definition)) throw new Error(`${i + 1}. kural başka bir kuralla çakışıyor: aynı kapsam ve amaç için tek kural kullanın.`);
        definitions.add(definition);
        if (!r.aciklama.trim()) throw new Error(`${i + 1}. kural açıklaması boş olamaz.`);
        const normal = parse(r.normalEsik), lower = r.kontrolTuru === 2 && r.altEsik != null && String(r.altEsik).trim() !== "" ? parse(r.altEsik) : null;
        let count = null;
        if (r.kontrolTuru !== 1) { if (!/^\d+$/.test(String(r.donemSayisi)) || !Number.isSafeInteger(Number(r.donemSayisi)) || Number(r.donemSayisi) > 2147483647 || Number(r.donemSayisi) < (r.kontrolTuru === 3 ? 1 : 0)) throw new Error(`${i + 1}. kuralın dönem sayısını kontrol edin.`); count = Number(r.donemSayisi); }
        if (lower != null && lower > normal) throw new Error(`${i + 1}. alt sınır GANO sınırından büyük olamaz.`);
        return { kuralKimligi: r.kuralKimligi, kapsam: r.kapsam, amac: r.amac, kontrolTuru: r.kontrolTuru, normalEsik: normal, altEsik: lower, donemSayisi: count, aciklama: r.aciklama };
      });
      const number = value => { if (value === "" || value == null) return null; if (!/^\d+([.,]\d{1,2})?$/.test(String(value))) throw new Error("Sayısal sınırları kontrol edin."); return Number(String(value).replace(",", ".")); };
      numericRows = state.numericDraft.map(r => ({ olcut: Number(r.olcut), mufredatId: r.mufredatId || null, altSinir: number(r.altSinir), ustSinir: number(r.ustSinir), dayanak: r.dayanak }));
    } catch (e) { say(e.message, true); return; }
    busy = true; removal = null; render(); say(""); let success = false;
    try {
      const response = await fetch(root.dataset.saveUrl, { method: "POST", credentials: "same-origin", headers: {
        "Content-Type": "application/json", "RequestVerificationToken": form.querySelector('[name="__RequestVerificationToken"]').value
      }, body: JSON.stringify({ programTuru: id, beklenenSurumId: state.version, kurallar: rows, sayisalKurallar: numericRows }) });
      if (!response.ok) { let text = response.status === 409 ? "Kurallar değişti veya geçmiş sürüm doğrulanamadı. Taslağınız korunuyor; sayfayı yenileyin." : "Kayıt tamamlanamadı; taslağınız korunuyor.";
        if (response.status === 400 && response.headers.get("content-type")?.includes("application/json")) { const value = await response.json(); if (typeof value === "string") text = value; }
        throw new Error(text); }
      const data = await response.json(); if (!data.surumId) throw new Error("Kayıt sonucu doğrulanamadı. Tekrar göndermeden sayfayı yenileyin.");
      state.version = data.surumId; state.saved = copy(state.draft); state.numericSaved = copy(state.numericDraft); success = true;
    } catch (e) { say(safeError(e) || "Bağlantı kesildi; otomatik tekrar yapılmadı.", true); }
    finally { busy = false; render(); }
    if (success) { await read(id, true); if (!current().error) say("Kurallar kaydedildi."); }
  });
  void read(1);
})();
