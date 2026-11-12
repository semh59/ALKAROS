# V1-RMD-1022 - Iptal / iade dugmesi iade bolumu acik ekrana gider

Duzeltme: TableOrderBridge Iptal / iade -> odeme ekrani ?billId=..&open=refund (Bol hesap bolme ekraninda kalir); split-payment.js open=refund isareti hazir ve odendi ekranlarinda iade katlanirini acar ve ilk dokunusa kadar gorunur alana kaydirir.
Kanit: bridge testi + yeni split-payment-open-refund-fold.test.js 4 test once kirmizi; TableOrderBridge 26/26, split-payment* 24 dosya 265/265; client-build-gate exit 0; gercek tarayici M-02: dugme -> ...index.html?billId=..&open=refund; odeme ekraninda "Iade ve iptal" acik, 1366x768'de gorunur alanda (iptal-iade-bolumu-acik.png). Mutasyon 4/4 killed.
Qa sonrasi: kaydirma klavye/odak/degisiklik olaylarinda da durur (test), yetkisi olmayana olmayan dugme gosterilmez (test); TableOrderBridge 27/27, split-payment* 267/267, gate exit 0.
