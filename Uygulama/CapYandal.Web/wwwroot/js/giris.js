"use strict";
// Yeni parola sayfasından taşınmış veya geçmişten geri yüklenmiş değer gösterilmez.
const newPasswordEntry = document.querySelector('[data-clear-password="true"]');
if (newPasswordEntry) {
    newPasswordEntry.value = '';
    window.addEventListener('pageshow', () => { newPasswordEntry.value = ''; });
}
