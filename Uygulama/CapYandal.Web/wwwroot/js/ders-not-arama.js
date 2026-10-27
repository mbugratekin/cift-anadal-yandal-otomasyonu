"use strict";
// Satırın boş bölümü de aynı öğrenciye gider; gerçek bağlantılar klavye erişimini korur.
document.querySelectorAll('.grade-student-roster tbody tr:not(.grade-student-empty)').forEach(row => {
    row.addEventListener('click', event => {
        if (event.target.closest('a,button,input,select,textarea') || window.getSelection()?.toString()) return;
        const link = row.querySelector('a[href]');
        if (link) window.location.assign(link.href);
    });
});
document.querySelector('.grade-student-search')?.addEventListener('input', event => {
    const term = event.target.value.trim().toLocaleLowerCase('tr-TR');
    const rows = [...document.querySelectorAll('.grade-student-roster tbody tr:not(.grade-student-empty)')];
    rows.forEach(row => { row.hidden = !`${row.cells[0].textContent} ${row.cells[1].textContent}`.toLocaleLowerCase('tr-TR').includes(term); });
    document.querySelector('.grade-student-empty').hidden = rows.some(row => !row.hidden);
});
