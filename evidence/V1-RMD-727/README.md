# V1-RMD-727 - İade akışı sertleştirme (603 B002, B016, B017, B018)

**Sonuç:** Aynı iade isteği ağ hatasından sonra tekrar gönderilirse artık ikinci nakit iade çıkmıyor; sunucu ilk isteği aynen döndürüyor. İade isteği ve onayı denetim kaydına yazılıyor, iade listesi hesaba göre süzülüyor ve sayfalanıyor, kalem bazlı iadede yuvarlama farkı ödemeyi aşmıyor.

## Değişiklik
- Migration 207: `payments.refund_requests.idempotency_key` + `(bill_id, idempotency_key)` benzersiz dizini (manifest, order.json, testler güncel).
- `PaymentRefundStore.RequestAsync`: `X-Idempotency-Key` zorunlu (en çok 128 karakter); hesap kilidi altında aynı anahtar aynı ödeme için kayıtlı isteği döndürür, başka ödeme için 409 `IDEMPOTENCY_KEY_REUSED`. Depo yalnız açık-istek benzersiz ihlalini "zaten istendi" sayar.
- `refund.requested` ve `refund.approved` denetim olayları (yönetici ve bütçe yolu).
- Liste: `billId`, `before`, `limit` (en çok 200), yeni→eski sıralı.
- Kalem toplamı ödemeyi en çok kalem sayısı x 0,01 aşarsa ödeme tutarına indirilir; daha fazlası reddedilir.
- İstemciler (Kasa `split-payment.js`, PosTerminal `refundApi.ts`) aynı isteğin tekrarında aynı anahtarı kullanır; PosTerminal listeyi `billId` ile ister. Idempotency allowlist girdisi kaldırıldı.

## Kanıt (logs/)
- Host Billing iade testleri 34/34 (yeni 4: aynı anahtarla tekrar tek ödeme ve tek kayıt, başka ödemede 409, anahtarsız 400, iki denetim olayı, liste süzme/sayfalama).
- Mimari ApiConventions 5/5; MigrationComposition Manifest 17/17, Migrations 16/16; PosTerminal refundApi 10/10 (yeni: tekrar aynı anahtar).
- Mutasyon (`mutations.log`): 7/7 KILLED, kaynak geri yüklendi.

## Bilinçli yapılmayanlar (603 kapsamında kalan alt iddialar)
Fiş numarası benzersizliği, isteyen=tamamlayan kısıtı, bütçe serbest bırakma/iptal ucu, bütçe PUT'unda yönetici rolü şartı (PO çelişkisi), hata iletisi eşlemesi (733: anahtarsız istek şimdilik genel "iade bilgisi geçersiz" iletisini alır). Kasa istemcisinin StaticApps testleri bu görevin yüzeyi dışında; yeni davranış için test eklenmedi.
