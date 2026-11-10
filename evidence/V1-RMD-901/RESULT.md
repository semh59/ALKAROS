# V1-RMD-901 sonucu

Yönetici, iptal ya da yanıt kaybıyla yanan orijinal fiş hakkını gerekçeyle geri verebiliyor.

## Değişiklik
- POST /api/v1/terminals/{terminal}/billing/bills/{bill}/receipt/original-reset (DualScreenApplication.Receipt.cs): yetki payments.refund.approve (iade onayı kalıbı; kasiyerde yok), gerekçe 1-500 karakter (kırpılır), hesap satırı kilidi altında `receipt_first_printed_at/_by` temizlenir (IBillReceiptPrintRepository.ResetOriginalAsync), aynı işlemde denetim kaydı `bill.receipt.original.reset` (gerekçe, önceki basım zamanı ve basan). Basılmamış fiş: 409 RECEIPT_NOT_PRINTED; bilinmeyen hesap 404; gerekçesiz 400 REASON_REQUIRED; hepsi Türkçe mesajlı.
- Cashier ödenmiş hesap ekranında "Orijinal fişi sıfırla (yönetici)" bölümü: zorunlu gerekçe alanı, "Orijinal hakkını geri ver" düğmesi, Türkçe hata sözlüğü (yetkisiz: "Bu işlem için yetkiniz yok.", RECEIPT_NOT_PRINTED), istek sürerken kilit. React kasada fiş yazdırma olmadığı için karşılığı yok. Ekran yetkiyi bilmez; yetkisiz kullanıcıya sunucu 403 verir.

## Kanıt
- Sunucu: BillReceiptHttpTests 24/24 gerçek Postgres (3 yeni: sıfırlama ve sonraki basımın yine orijinal olması + denetim kaydı, yetki/gerekçe/uzunluk/bilinmeyen hesap, basılmamış fiş). 4 sunucu mutantı öldü (talep silinmiyor, yanlış yetki, gerekçe zorunlu değil, uzunluk denetimi yok); red-1.log.
- İstemci: split-payment-receipt-reset.test.js 7/7; 10 istemci mutantı öldü (gerekçe yazımı yeniden çizimde korunur, sıfırlama sürerken yazdırma kilitli). StaticApps tamamı final-staticapps.log.
- Mutasyonlar: mutations.txt (hepsi "restored-identical").

## Açık (PO sorusu, değiştirilmedi)
- Yönetici kendi bastığı fişi kendisi sıfırlayıp yeniden bastırabilir; her iki işlem denetim kaydında ayrı görünür (bill.receipt.printed / bill.receipt.original.reset, gerekçe ve önceki basan ile) ama sayaç/üst sınır/ikinci kişi şartı yok. Sıfırlamayı iade onayındaki "kendi talebini onaylayabilir" ayarına bağlamak ya da sıfırlama sayısını sınırlamak ürün kararıdır.
- Bölüm ekranda her kasiyere görünür (ekran yetkiyi bilmez); yetkisize sunucu 403 verir.
