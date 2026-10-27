"use strict";
// Sayısal uyarı görünümü — yalnız sunucunun sakladığı kontrol kopyasını gösterir, akademik karar üretmez.
document.querySelectorAll(".numeric-check").forEach(root => {
  const message = root.querySelector(".numeric-check-message"), list = root.querySelector(".numeric-check-results"), form = root.querySelector("form");
  const table = root.querySelector('.numeric-results-scroll');
  const number = value => value == null ? "—" : Number(value).toLocaleString("tr-TR");
  function show(data) {
    list.replaceChildren();
    table.hidden = true;
    if (!data.kontrol) { message.textContent = "Henüz sayısal koşul kontrolü yapılmamış."; return; }
    message.textContent = data.yeniKuralVar ? "Kurallar güncellendi; burada önceki değerlendirme gösteriliyor." : "";
    if (!data.kontrol.sonuclar.length) message.textContent = "Bu program/müfredat için sayısal koşul tanımlanmamış.";
    table.hidden = !data.kontrol.sonuclar.length;
    data.kontrol.sonuclar.forEach(r => {
      const item = document.createElement('tr'), condition=document.createElement('th');
      const name=document.createElement('span'),value=document.createElement('td'),status=document.createElement('td'),explanation=document.createElement('small'),limits=document.createElement('td'),source=document.createElement('small');
      condition.scope='row'; name.textContent=r.ad;value.textContent=number(r.deger);value.className='numeric-value';
      status.textContent=r.durum==='Uyari'?'Koşul sağlanmıyor':r.durum==='Aralikta'?'Sınır sağlanıyor':'Değerlendirilemedi';status.className='numeric-status';
      item.dataset.state=r.durum;explanation.textContent=r.aciklama;
      limits.textContent=r.altSinir != null && r.ustSinir != null ? `${number(r.altSinir)} – ${number(r.ustSinir)}` : r.altSinir != null ? `En az ${number(r.altSinir)}` : r.ustSinir != null ? `En fazla ${number(r.ustSinir)}` : '—';
      source.textContent=r.dayanak || '';
      condition.append(name,source);status.append(explanation);item.append(condition,value,limits,status);
      list.append(item);
    });
  }
  async function load() {
    try { const url = new URL(root.dataset.readUrl, location.href); url.searchParams.set("programId", root.dataset.programId); url.searchParams.set("donemId", root.dataset.periodId);
      const response = await fetch(url, { credentials: "same-origin", cache: "no-store" }); if (!response.ok || !response.headers.get("content-type")?.includes("application/json")) throw new Error(); show(await response.json());
    } catch { message.textContent = "Sayısal kontrol sonucu okunamadı; sayfayı yenileyin."; }
  }
  form?.addEventListener("submit", async event => {
    event.preventDefault(); const button = form.querySelector("button"); if (button.disabled) return; button.disabled = true;
    try { const response = await fetch(root.dataset.checkUrl, { method: "POST", credentials: "same-origin", body: new FormData(form) });
      if (!response.ok || !response.headers.get("content-type")?.includes("application/json")) throw new Error(); show(await response.json());
    } catch { message.textContent = "Kontrol kaydedilemedi. Kural sürümünü ve dönemi doğrulayıp tekrar deneyin."; }
    finally { button.disabled = false; }
  });
  void load();
  document.addEventListener('academic-record-saved', load);
});
