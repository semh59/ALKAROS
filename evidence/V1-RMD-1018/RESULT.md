# V1-RMD-1018 - Odeme sonucu ekraninda iade bolumu kapali

Tekrar: gercek ekranda odenmis hesapta iade formlari acik kart olarak basari mesajinin altinda duruyordu.
Duzeltme: split-payment.js renderPaid iade bolumunu kapali "Iade" katlanir basligina alir; bekleyen iade istegi/hata/bildirim/onay varsa acik gelir; katlanir dinleyicisi bindFolds ortak islevine alindi (ready ekrani da kullanir).
Kanit: yeni dosya split-payment-paid-refund-fold.test.js 3 test once kirmizi; split-payment* 23 dosya 259/259; gercek tarayici (docker kasa + calisma agaci JS): Hesap Odendi, "Iade" kapali (odendi-iade-kapali.png). Mutasyon 3/3 killed.
Qa sonrasi: odendi ekrani kendi katlanir anahtarini (refundPaid) kullanir; split-payment* 23 dosya 260/260.
