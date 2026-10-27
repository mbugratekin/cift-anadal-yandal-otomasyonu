"use strict";
// Konuşma listesi — yalnız sunucunun yetkili öğrenci adları ve okunmamış sayıları yerelde süzülür.
const chatSidebar=document.querySelector('.chat-sidebar');
// Yalnız öğrenci listesinin kaydırma konumu tutulur; seçilen konuşma her zaman son mesaj ve gönderme alanıyla açılır.
if (chatSidebar) {
    const key = 'cap-sohbet-konum';
    try {
        const saved = JSON.parse(sessionStorage.getItem(key) || 'null');
        sessionStorage.removeItem(key);
        if (saved && Date.now() - saved.time < 60000 && Number.isFinite(saved.list)) {
            requestAnimationFrame(() => requestAnimationFrame(() => {
                chatSidebar.querySelector('.chat-student-list').scrollTop = saved.list;
            }));
        }
    } catch { /* Depolama kapalıysa normal gezinme sürer. */ }
    chatSidebar.addEventListener('click', event => {
        if (!event.target.closest('a.chat-person')) return;
        try { sessionStorage.setItem(key, JSON.stringify({ list: chatSidebar.querySelector('.chat-student-list').scrollTop, time: Date.now() })); } catch { }
    });
}
// Görünür alanı iki bağımsız kaydırma bölmesine ayırır; menü yüksekliği dar ekranda da hesaba katılır.
const chatWorkspace=document.querySelector('.chat-workspace');
if(chatWorkspace){
    function fitChat(){chatWorkspace.style.setProperty('--chat-height',Math.max(320,innerHeight-chatWorkspace.getBoundingClientRect().top-16)+'px');}
    fitChat();window.addEventListener('resize',fitChat);document.fonts?.ready.then(fitChat);
}
if(chatSidebar){
    const search=chatSidebar.querySelector('input[type=search]'), people=[...chatSidebar.querySelectorAll('.chat-person')], empty=chatSidebar.querySelector('.chat-list-empty');
    let unreadOnly=chatSidebar.dataset.unreadOnly==='true';
    function filterPeople(){
        const words=search.value.trim().toLocaleLowerCase('tr-TR').split(/\s+/).filter(Boolean);
        let count=0;
        for(const person of people){person.hidden=(unreadOnly&&Number(person.dataset.unread)===0)||!words.every(word=>person.dataset.search.toLocaleLowerCase('tr-TR').includes(word));if(!person.hidden)count++;}
        empty.hidden=count>0;empty.textContent=unreadOnly?'Okunmamış mesaj yok.':'Öğrenci bulunamadı.';
        chatSidebar.querySelectorAll('[data-unread-filter]').forEach(button=>button.setAttribute('aria-pressed',String((button.dataset.unreadFilter==='true')===unreadOnly)));
    }
    search.addEventListener('input',filterPeople);
    chatSidebar.querySelectorAll('[data-unread-filter]').forEach(button=>button.addEventListener('click',()=>{unreadOnly=button.dataset.unreadFilter==='true';filterPeople();}));
    filterPeople();
}
const chatForm=document.querySelector('.chat-compose'),chatMessages=document.querySelector('.chat-messages');
if(chatForm&&chatMessages) {
    function revealComposer() {
        requestAnimationFrame(()=>requestAnimationFrame(()=>{
            chatForm.scrollIntoView({block:'end',inline:'nearest'});
            chatMessages.scrollTop=chatMessages.scrollHeight;
            window.scrollTo({top:document.documentElement.scrollHeight,behavior:'instant'});
        }));
    }
    function scrollToBottom(force=false) {
        if(!force&&chatMessages.dataset.currentPage!=='0')return;
        requestAnimationFrame(()=>{
            requestAnimationFrame(()=>{
                chatMessages.scrollTop=chatMessages.scrollHeight;
            });
        });
    }
    if(chatMessages.dataset.currentPage==='0') {
        scrollToBottom(true);
        if(chatSidebar?.querySelector('.chat-person[aria-current="page"]'))
            revealComposer();
    }
    let sending=false,polling=false,epoch=0,unknownSend=false,signature=null;
    let acknowledged=null;
    let acknowledging=false;
    const error=chatForm.querySelector('.chat-error'),field=chatForm.elements.Metin,sendStatus=chatForm.querySelector('.chat-send-status');
    const attachmentName=chatForm.querySelector('.chat-attachment-name'),attachmentRemove=chatForm.querySelector('.chat-attachment-remove');
    function attachmentLabel(){const name=chatForm.elements.Ek?.files?.[0]?.name||'';if(attachmentName)attachmentName.textContent=name;if(attachmentRemove)attachmentRemove.hidden=!name;}
    chatForm.elements.Ek?.addEventListener('change',attachmentLabel);
    attachmentRemove?.addEventListener('click',()=>{chatForm.elements.Ek.value='';attachmentLabel();});
    function busy() {
        chatForm.querySelector('.chat-send').disabled=sending||unknownSend;
        field.disabled=sending;
        if(chatForm.elements.Ek)chatForm.elements.Ek.disabled=sending;
    }
    function render(row) {
        const article=document.createElement('article');article.className='chat-message'+(row.benimMi?' is-own':'');
        article.tabIndex=0;
        article.dataset.messageId=row.id;article.setAttribute('aria-label',row.gonderen);
        if(!row.benimMi){const sender=document.createElement('strong');sender.className='chat-sender';sender.textContent=row.gonderen;article.append(sender);}
        const content=document.createElement('p');content.textContent=row.metin;
        const time=document.createElement('time'),date=document.createElement('span'),hour=document.createElement('span'),when=new Date(row.ani);
        time.dateTime=row.ani;date.textContent=when.toLocaleDateString('tr-TR',{timeZone:'Europe/Istanbul',day:'numeric',month:'long'});
        hour.textContent=when.toLocaleTimeString('tr-TR',{timeZone:'Europe/Istanbul',hour:'2-digit',minute:'2-digit'}).replace(':','.');time.append(date,hour);if(row.metin)article.append(content);
        if(row.ek){const link=document.createElement('a'),url='/Sohbet/Ek?id='+encodeURIComponent(row.ek.id),pdf=/\.pdf$/i.test(row.ek.dosyaAdi);link.className='chat-attachment-link';link.href=url+(pdf?'&onizle=true':'');link.textContent=row.ek.dosyaAdi+' ('+Math.ceil(row.ek.boyutBayt/1024)+' KB)'+(pdf?' · Önizle':'');if(pdf){link.target='_blank';link.rel='noopener';}article.append(link);if(pdf){const download=document.createElement('a');download.className='chat-attachment-link';download.href=url;download.textContent='İndir';article.append(download);}}
        article.append(time);
        return article;
    }
    async function acknowledge(rows) {
        if(document.hidden||acknowledging||!chatForm.dataset.readUrl)return;
        const ids=rows.filter(x=>!x.benimMi).map(x=>x.id),key=ids.join(',');
        if(!ids.length||key===acknowledged)return;
        const data=new FormData();data.set('OgrenciId',chatForm.elements.OgrenciId.value);
        const token=chatForm.querySelector('[name="__RequestVerificationToken"]');if(!token)return;
        data.set('__RequestVerificationToken',token.value);ids.forEach(id=>data.append('Mesajlar',id));
        acknowledging=true;
        try {
            const response=await fetch(chatForm.dataset.readUrl,{method:'POST',body:data,credentials:'same-origin'});
            if(response.ok&&!response.redirected){
                const result=await response.json();
                if(!result.okundu||!Number.isInteger(result.konusmaOkunmamis)||result.konusmaOkunmamis<0)return;
                acknowledged=key;
                const person=chatSidebar?.querySelector('.chat-person[aria-current="page"]');
                if(person){person.dataset.unread=String(result.konusmaOkunmamis);const count=person.querySelector('.chat-person-count');if(count){count.textContent=String(result.konusmaOkunmamis);count.hidden=result.konusmaOkunmamis===0;count.setAttribute('aria-label',result.konusmaOkunmamis+' okunmamış mesaj');}}
                document.dispatchEvent(new Event('sohbet-okundu'));
            }
        }catch{}finally{acknowledging=false;}
    }
    async function refresh(force=false) {
        if(document.hidden||sending||polling)return false;
        polling=true;const started=epoch;
        try {
            const response=await fetch(chatMessages.dataset.url,{credentials:'same-origin',cache:'no-store'});
            if(!response.ok||!response.headers.get('content-type')?.includes('application/json'))return false;
            const rows=await response.json();if(!Array.isArray(rows)||epoch!==started||sending)return false;
            const current=JSON.stringify(rows.map(x=>[x.id,x.sabitlendiMi,x.gonderen,x.metin,x.ek?.id,x.ek?.dosyaAdi,x.ek?.boyutBayt]));
            if(!force&&current===signature){acknowledge(rows);return true;}
            const firstLoad=signature===null;
            const nearBottom=firstLoad || chatMessages.scrollHeight-chatMessages.scrollTop-chatMessages.clientHeight<40;
            const focusId=document.activeElement?.closest('[data-message-id]')?.dataset.messageId;
            const elements=rows.map(render);
            if(!elements.length){const empty=document.createElement('p');empty.className='chat-empty';empty.textContent='Henüz mesaj yok.';elements.push(empty);}
            chatMessages.replaceChildren(...elements);signature=current;acknowledge(rows);
            if((nearBottom||force)&&chatMessages.dataset.currentPage==='0')scrollToBottom(force);
            if(firstLoad&&chatMessages.dataset.currentPage==='0'&&chatSidebar?.querySelector('.chat-person[aria-current="page"]'))revealComposer();
            if(focusId){const target=Array.from(chatMessages.querySelectorAll('[data-message-id]')).find(x=>x.dataset.messageId===focusId);(target||field).focus({preventScroll:true});}
            busy();return true;
        }catch{return false;}finally{polling=false;}
    }
    function fitMessage(){field.style.height='auto';field.style.height=Math.max(44,field.scrollHeight+2)+'px';}
    field.addEventListener('input',fitMessage);
    field.addEventListener('keydown',event=>{
        if(event.key==='Enter'&&!event.shiftKey){
            event.preventDefault();
            if(field.value.trim()||chatForm.elements.Ek?.files?.[0]){
                if(!sending&&!unknownSend){
                    if(typeof chatForm.requestSubmit==='function')chatForm.requestSubmit();
                    else chatForm.dispatchEvent(new Event('submit',{cancelable:true}));
                }
            }
        }
    });
    window.addEventListener('resize',fitMessage);fitMessage();document.fonts?.ready.then(fitMessage);
    chatForm.addEventListener('submit',async event=>{
        event.preventDefault();if(sending||unknownSend||!chatForm.reportValidity())return;
        const attached=chatForm.elements.Ek?.files?.[0];error.textContent='';
        if(!field.value.trim()&&!attached){error.textContent='Mesaj yazın veya dosya seçin.';field.focus();return;}
        const data=new FormData(chatForm);sending=true;epoch++;busy();
        chatForm.setAttribute('aria-busy','true');if(sendStatus)sendStatus.textContent='';
        const uploadNotice = setTimeout(()=>{if(sending&&attached&&sendStatus)sendStatus.textContent='Belge yükleniyor…';},700);
        let sent=false;
        try {
            const response=attached ? await new Promise((resolve,reject)=>{
                const request=new XMLHttpRequest();request.open('POST',chatForm.action);request.setRequestHeader('Accept','application/json');
                request.addEventListener('load',()=>resolve(new Response(request.responseText,{status:request.status,headers:{'Content-Type':request.getResponseHeader('Content-Type')||'text/plain'}})));
                request.addEventListener('error',reject);request.addEventListener('abort',reject);request.send(data);
            }) : await fetch(chatForm.action,{method:'POST',body:data,credentials:'same-origin',headers:{Accept:'application/json'}});
            if(response.ok){const result=await response.json();if(!result.gonderildi)throw new Error('Belirsiz sonuç');
                sent=true;if(sendStatus)sendStatus.textContent='';field.value='';if(chatForm.elements.Ek)chatForm.elements.Ek.value='';attachmentLabel();field.style.height='auto';chatForm.elements.IstekId.value=crypto.randomUUID();}
            else {
                let detail = [400,409,503].includes(response.status)?await response.text():'Mesaj gönderilemedi. Oturumunuzu ve yetkinizi kontrol edin.';
                try { const value=JSON.parse(detail);if(typeof value==='string')detail=value;else if(value.hata)detail=value.hata; } catch {}
                error.textContent=detail;
                unknownSend=response.status>=500||response.status===409;
            }
        }catch{unknownSend=true;error.textContent='Gönderim sonucu doğrulanamadı. Otomatik tekrar yapılmadı; sayfayı yenileyin ve mesajları kontrol edin.';}
        finally{clearTimeout(uploadNotice);sending=false;if(sendStatus)sendStatus.textContent='';busy();}
        if(sent){if(chatMessages.dataset.currentPage!=='0'){window.location.reload();return;}
            if(!await refresh(true))error.textContent='Mesaj gönderildi; liste yenilenemedi. Mesajı tekrar göndermeyin, sayfayı yenileyin.';
            else scrollToBottom(true);
            field.focus({preventScroll:true});}
        if(sendStatus)sendStatus.textContent='';chatForm.removeAttribute('aria-busy');
    });
    void refresh();
    document.addEventListener('visibilitychange',()=>{if(!document.hidden)void refresh();});
    setInterval(()=>refresh(),10000);
}
