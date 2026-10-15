# V1-RMD-530 kanıtı

- Kanal raporu ekranı: her satırda ve toplamda "İade edilen" sütunu (`refundedValue`).
- Ürün kârlılığı ekranı: `unallocatedRefunds` sıfırdan büyükse "Ürün seçilmeden yapılan iadeler ..." satırı; sıfırsa görünmez.
- Test: ilgili iki ekran 17/17 (jsdom, `test-runs.log`); `tsc --noEmit` temiz; mutasyon 7/7 öldü (`mutations.log`).
- Doğrulanmadı: gerçek tarayıcıda ve gerçek Host'a karşı görsel kontrol yapılmadı.
