'use strict';
const transcriptPanel=document.getElementById('transcript-preview');
if(transcriptPanel){
    let opener=null;
    const frame=transcriptPanel.querySelector('iframe'),close=transcriptPanel.querySelector('.transcript-close');
    function hide(){transcriptPanel.hidden=true;frame.removeAttribute('src');opener?.focus();}
    document.addEventListener('click',event=>{
        const link=event.target.closest('a[data-transcript-preview="true"]');
        if(!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)return;
        const url=new URL(link.href,location.href);if(url.origin!==location.origin)return;
        event.preventDefault();opener=link;frame.src=url.href;
        transcriptPanel.querySelector('.transcript-new-tab').href=url.href;
        transcriptPanel.querySelector('strong').textContent=link.textContent;
        transcriptPanel.hidden=false;close.focus();
    });
    close.addEventListener('click',hide);
    document.addEventListener('keydown',event=>{if(event.key==='Escape'&&!transcriptPanel.hidden){event.preventDefault();hide();}});
}
