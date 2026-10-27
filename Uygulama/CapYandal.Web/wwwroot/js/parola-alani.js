(() => {
    'use strict';

    const temizle = alan => {
        const baslangic = alan.selectionStart;
        const onceki = alan.value;
        const temiz = onceki.replace(/\s/gu, '');
        if (temiz === onceki) return;
        alan.value = temiz;
        if (baslangic !== null) {
            const silinen = onceki.slice(0, baslangic).length - temiz.slice(0, baslangic).length;
            alan.setSelectionRange(Math.max(0, baslangic - silinen), Math.max(0, baslangic - silinen));
        }
    };

    document.querySelectorAll('input[type="password"],input[name="KullaniciAdi"]').forEach(alan => {
        alan.addEventListener('beforeinput', olay => {
            if (olay.data && /\s/u.test(olay.data)) olay.preventDefault();
        });
        alan.addEventListener('keydown', olay => {
            if (olay.key.length === 1 && /\s/u.test(olay.key)) olay.preventDefault();
        });
        alan.addEventListener('paste', olay => {
            const gelen = olay.clipboardData?.getData('text');
            if (!gelen || !/\s/u.test(gelen)) return;
            olay.preventDefault();
            const baslangic = alan.selectionStart ?? alan.value.length;
            const bitis = alan.selectionEnd ?? baslangic;
            alan.setRangeText(gelen.replace(/\s/gu, ''), baslangic, bitis, 'end');
        });
        alan.addEventListener('input', () => temizle(alan));
    });
})();
