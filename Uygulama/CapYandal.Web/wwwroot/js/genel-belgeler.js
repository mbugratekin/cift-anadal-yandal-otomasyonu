"use strict";
document.querySelectorAll(".general-document-form:not(.evrak-upload-form)").forEach(form => {
    const input = form.querySelector('input[type="file"]');
    const filename = form.querySelector(".general-document-filename");
    const message = form.querySelector(".document-form-message");
    let busy = false, uncertain = false;
    function scope() { const label=form.querySelector('.protocol-title');if(!label)return;label.hidden=form.elements.tur.value!=='Protokoller';label.querySelector('input').required=!label.hidden; }
    form.elements.tur?.addEventListener('change',scope);scope();
    form.querySelector('[name=protokolAdi]')?.addEventListener('change',()=>{if(input.files.length&&!busy&&!uncertain&&form.checkValidity())form.requestSubmit();});
    input.addEventListener("change", () => {
        if (busy) return;
        message.textContent = "";
        const file = input.files[0];
        if (!file) return;
        if (input.files.length !== 1 || !/\.(pdf|docx)$/i.test(file.name) || file.size <= 0 || file.size > 10485760) {
            input.value = "";
            filename.textContent = "";
            message.textContent = "Tek bir PDF veya Word (.docx) seçin; dosya boş olmamalı ve 10 MB’ı aşmamalı.";
            input.focus();
            return;
        }
        filename.textContent = file.name;
        form.requestSubmit();
    });
    form.addEventListener("submit", async event => {
        event.preventDefault();if(busy||uncertain||!form.reportValidity())return;
        const body=new FormData(form);busy=true;input.disabled=true;message.textContent='%0';
        try {
            const response=await new Promise((resolve,reject)=>{
                const request=new XMLHttpRequest();request.open('POST',form.action);request.setRequestHeader('Accept','application/json');
                request.upload.addEventListener('progress',e=>{if(e.lengthComputable)message.textContent='%'+Math.floor(e.loaded/e.total*100);});
                request.onload=()=>resolve({status:request.status,text:request.responseText});request.onerror=reject;request.onabort=reject;request.send(body);
            });
            const result=JSON.parse(response.text);
            if(response.status!==200||result.kaydedildi!==true){uncertain=response.status>=500;throw new Error(result.hata||'Belge kaydedilemedi.');}
            location.reload();
        } catch(error) {
            if(!(error instanceof Error)||error instanceof SyntaxError)uncertain=true;
            message.textContent=error instanceof Error?error.message:'Sonuç doğrulanamadı; tekrar göndermeden sayfayı yenileyin.';
        } finally { busy=false;input.disabled=uncertain; }
    });
});
