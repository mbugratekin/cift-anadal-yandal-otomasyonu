"use strict";
function bindDocumentUploads(){ document.querySelectorAll(".new-document-upload:not([data-upload-bound])").forEach(form => {
    form.dataset.uploadBound='true';
    const file=form.querySelector('[name=file]'), options=form.querySelector('.new-upload-options'),
        choice=form.querySelector('select'), token=form.querySelector('[name=surum]'),
        kind=form.querySelector('[name=tur]'), message=form.querySelector('.new-upload-message');
    let busy=false, uncertain=false;
    function selectTarget(){const selected=choice.selectedOptions[0];kind.value=selected.dataset.tur;token.disabled=!selected.value;token.value=selected.dataset.surum||"";choice.disabled=false;}
    choice.addEventListener('change',selectTarget);
    file.addEventListener('change',()=>{
        if(busy||uncertain)return;
        options.hidden=!file.files.length;
        form.querySelector('.selected-file').textContent=file.files[0]?.name||"";
        selectTarget();
    });
    form.addEventListener('submit',async event=>{
        event.preventDefault();if(busy||uncertain||!form.reportValidity())return;
        const selected=file.files[0];
        if(!selected||!/\.(pdf|docx)$/i.test(selected.name)||selected.size<=0||selected.size>10485760){message.textContent="Tek PDF veya Word (.docx) seçin; dosya 10 MB’ı aşmamalı.";return;}
        selectTarget();const data=new FormData(form);if(!choice.value)data.delete('genelBelgeId');
        busy=true;file.disabled=true;choice.disabled=true;form.querySelector('button').disabled=true;message.textContent="Belge güvenlik kontrolünden geçirilerek yükleniyor…";
        try{const response=await fetch(form.action,{method:'POST',body:data,headers:{Accept:'application/json'}});uncertain=true;const result=await response.json();
            if(response.ok&&result.kaydedildi===true){location.reload();return;}
            uncertain=true;message.textContent=(result.hata||"Sonuç doğrulanamadı.")+" Tekrar göndermeden sayfayı yenileyin.";
        }catch{uncertain=true;message.textContent="Sonuç doğrulanamadı; tekrar göndermeden sayfayı yenileyin.";}
        finally{busy=false;file.disabled=uncertain;choice.disabled=uncertain;form.querySelector('button').disabled=uncertain;}
    });
}); }
bindDocumentUploads();
document.addEventListener('general-documents-updated',bindDocumentUploads);
document.addEventListener('submit',async event=>{
    const form=event.target;
    if(!form.matches('.new-archive-form'))return;
    event.preventDefault();
    if(form.dataset.busy)return;
    const data=new FormData(form),button=form.querySelector('button');
    form.dataset.busy='true';button.disabled=true;
    const root=form.closest('.documents-new');
    let status=root.querySelector('.archive-operation-status');
    if(!status){status=document.createElement('p');status.className='archive-operation-status';status.setAttribute('role','alert');root.prepend(status);}
    status.textContent='';
    try{
        const response=await fetch(form.action,{method:'POST',body:data,credentials:'same-origin',headers:{Accept:'application/json'}});
        const result=await response.json();
        if(!response.ok||!result.kaydedildi)throw new Error(result.hata||'İşlem tamamlanamadı.');
        const refresh=await fetch(location.href,{credentials:'same-origin',cache:'no-store'});
        if(!refresh.ok||refresh.redirected)throw new Error('Kaydedildi; güncel listeyi görmek için sayfayı yenileyin.');
        const fresh=new DOMParser().parseFromString(await refresh.text(),'text/html');
        const replacements=['.document-list','.new-document-archive'].map(selector=>[root.querySelector(selector),fresh.querySelector(selector)]);
        if(replacements.some(([old,next])=>!old||!next))throw new Error('Kaydedildi; güncel listeyi görmek için sayfayı yenileyin.');
        const y=scrollY,archiveOpen=root.querySelector('.new-document-archive')?.open;
        for(const [old,next] of replacements)old.replaceWith(next);
        root.querySelector('.new-document-archive').open=archiveOpen;
        document.dispatchEvent(new Event('general-documents-updated'));
        window.scrollTo(0,y);
    }catch(error){status.textContent=error.message||'Sonuç doğrulanamadı; tekrar göndermeden sayfayı yenileyin.';}
    finally{delete form.dataset.busy;button.disabled=false;}
});
