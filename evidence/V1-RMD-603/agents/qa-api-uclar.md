# qa-api-uclar - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 20/20 uç (bills 5, iade 7, split-design 7, fiş 1). PaymentTender uçları (6) kapsam dışı bırakıldı. Billing Host test projesi koşu kaydı yok (doğrulanamadı).

| No | Önem | Kanıt | Sorun | Etki |
|---|---|---|---|---|
| A1 | Orta | BillingSplitStore.cs:103-104,237-238; BillingSplitContracts.cs:108,134 | IdempotencyKey null/boş geçebilir; null anahtarlı istek anahtarı null olan satırla eşleşir, 200 döner ama yeni indirim/bahşiş yazılmaz. | Sessizce yutulan indirim/bahşiş |
| A2 | Orta | BillingSplitStore.cs:103-109; BillingSplitApplication.cs:596 | Aynı anahtar farklı gövde: doğrudan indirim/bahşiş 200 ile ilk kaydı döndürür (IDEMPOTENCY_KEY_CONFLICT yok; grant yolunda var). | Yanlış tutar kullanıcıya başarılı görünür |
| A3 | Orta | BillingSplitApplication.cs:142-171; BillingSplitStore.cs:61-74 | bills.discount yetkisiz rolde grant isteği açılırken değer/aralık/hesap/durum doğrulanmıyor; olmayan billId grant açabilir. | Yöneticiye geçersiz onay isteği |
| A4 | Orta | BillingSplitApplication.cs:177 | GRANT_DENIED 403 gövdesi ayrı şekilde (status/traceId yok), mesaj İngilizce "Discount request was denied."; V1-RMD-127 yalnız Orders'ı düzeltti. | Kural ihlali, İngilizce sızıntı |
| A5 | Orta | BillingSplitApplication.cs:586,598 | Tüm RefundInputException tek yanlış mesaj (gerekçe/not/fiş 1-500); ArgumentException/InvalidOperationException tek genel mesaj. | Yanlış yönlendirme |
| A6 | Orta | BillingSplitStore.cs:319-320; Application.cs:598 | İptal/reddedilmiş siparişten hesap açma 409 yerine 400; DI hataları 400 görünür. | Durum çakışması doğrulama hatası gibi |
| A7 | Orta | PostgresRefundRequestRepository.cs:86; Application.cs:286-298; refundApi.ts:126-127 | İade listesi LIMIT 200, sayfalama/billId süzgeci yok; istemci kendi süzer. | 200+ kayıtta onay bekleyen iade gizlenir |
| A8 | Orta | BillingSplitStore.cs:211-212; BillAdjustment.cs:282-285; migration 021 | Bahşiş üst sınırsız: DB taşması 503, decimal taşması 500; Notes uzunluk sınırsız. | Yanlış hata sınıfı |
| A9 | Orta | DualScreenApplication.Receipt.cs:61-62 | Fiş ucu 404'ü zarfsız boş gövdeyle veriyor. | Hata biçimi tutarsız |
| A10 | Orta | BillingSplitHttpTests.cs:110-133 | Bozuk JSON/boş gövde framework'ün zarfsız boş 400'ü; test kabul etmiş. | Hata biçimi sözleşmesi tutmuyor |
| A11 | Orta | tests Billing: NotFound 0 sonuç | 404/401/FEATURE_DISABLED/GRANT_DENIED/negatif bahşiş yolları testsiz; rota envanter testi yalnız 6 split rota. | Regresyon fark edilmez |
| A12 | Düşük | Application.cs:94-465 | 19 uçta terminal-read/write oran sınırı yok (fiş hariç). | Para uçlarında terminal bazlı koruma yok |
| A13 | Düşük | Application.cs:578 | NOT_FOUND mesajı hep "hesap". | Yanlış nesne adı |
| A14 | Düşük | BillCancellation.cs:57-58 | Yanıtı kaybolan iptalin tekrarı 409. | Ağ kopmasında yanlış hata |
| A15 | Düşük | idempotency_allowlist.json:460-475,760-775 | Allowlist gerekçeleri gerçek korumayla uyuşmuyor. | Doküman sapması |
| A16 | Düşük | BillingSplitContracts.cs:107-136 | Anahtar gövdede, X-Idempotency-Key başlığı yok (standart sapması). | Standart uyumsuz |
| A17 | Düşük | grep OpenApi = 0 | OpenAPI üretimi yok. | Sözleşme makine doğrulamasız |

Temiz: yetki kodları ApplicationPermissions ile eşleşiyor; kullanıcı/terminal/saat oturumdan; kendi işlemini onaylama engelli; iptal gerekçesi zorunlu + aynı işlemde audit; hata zarfı Türkçe sabit, ham istisna yok; allowlist çoğu doğru; eşzamanlı indirim korumalı; servis ücreti uca bağlı değil; istemci yalnız var olan rotaları çağırıyor; Billing'de şube kavramı yok.

Doğrulanamayanlar: Billing Host testi koşulmamış (PaymentTender.log kesik); ApiRouteAuthorizationTests / MutatingEndpointIdempotencyTests sonucu kayıtta yok; A1/A2/A8 canlı davranışı kod okuması; remaining-gaps-register'da Billing eşleşmesi yok.
