"use strict";
(() => {
 const header=document.querySelector('.panel-header'), toggle=header?.querySelector('.panel-mobile-toggle'), nav=header?.querySelector('.panel-nav');
 if(!header||!toggle||!nav)return;
 const mobile=window.matchMedia('(max-width:1050px)'), account=header.querySelector('.panel-account');
 function measure(){header.style.setProperty('--panel-mobile-top',header.querySelector('.panel-topbar').getBoundingClientRect().height+'px');}
 function close(returnFocus=false){header.classList.remove('mobile-nav-open');document.documentElement.classList.remove('mobile-menu-page-open');toggle.setAttribute('aria-expanded','false');toggle.setAttribute('aria-label','Menüyü aç');if(returnFocus)toggle.focus();}
 header.classList.add('mobile-nav-ready');
 toggle.addEventListener('click',()=>{if(header.classList.contains('mobile-nav-open')){close();return;}if(account)account.open=false;measure();header.classList.add('mobile-nav-open');document.documentElement.classList.add('mobile-menu-page-open');toggle.setAttribute('aria-expanded','true');toggle.setAttribute('aria-label','Menüyü kapat');});
 document.addEventListener('click',event=>{if(!header.contains(event.target))close();});
 document.addEventListener('keydown',event=>{if(event.key==='Escape'&&header.classList.contains('mobile-nav-open')){event.preventDefault();close(true);}});
 header.addEventListener('focusout',event=>{if(!header.contains(event.relatedTarget))close();});
 account?.addEventListener('toggle',()=>{if(account.open)close();});
 nav.querySelectorAll('a').forEach(link=>link.addEventListener('click',()=>close()));
 mobile.addEventListener('change',()=>close());
 window.addEventListener('pageshow',()=>close());
 window.addEventListener('resize',()=>{if(header.classList.contains('mobile-nav-open'))measure();});
 window.visualViewport?.addEventListener('resize',()=>{if(header.classList.contains('mobile-nav-open'))measure();});
})();
