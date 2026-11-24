# V1-RMD-1179 - Harici POS kartı ödeme türü: sunucu, migration, zorunlu fiş numarası, denetim izi

- Task ID: V1-RMD-1179
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Planned

## Goal

Ürün sahibi kararı (Semih, 2026-11-19): kart terminali sisteme bağlı değilken müşteri kartını bankanın ayrı POS cihazına okutuyor; kasiyer bu ödemeyi ALKAROS'ta "Harici POS kartı" olarak kayda alabilmelidir. Kod doğrulaması (`kasa-review/37-pilot-kart-yolu.md` ve bu görev): ödeme yöntemi `payments.payments` tablosunda tutulmaz, kayıtlardan türetilir (`PaymentMethodLookup.Sql`, rapor `MethodSql`); EFT/Havale doğru değildir (onay kutusu gerçeğe aykırı, fiş numarası yok, raporda kart toplamı yok); mevcut "terminal olmadan kart onayı" iki yetkili ve kilit ister, satış anı akışı değildir. Bu görev sunucu yarısıdır:

1. Migration 235: `payments.external_card_receipts` (ödeme başına bir satır: `payment_id` PK ve FK, `bill_id`, `terminal_id`, `slip_number`, `amount`, `recorded_by`, `recorded_at`). `slip_number` CHECK ile 4-32 karakter, büyük harf/rakam/`-`/`/`; `(bill_id, slip_number)` benzersiz (aynı hesapta aynı fiş numarası iki kez girilemez; farklı hesapta aynı numara POS parti numarası tekrarı olabileceğinden engellenmez). `payments.refund_requests.method` CHECK kuralına `ExternalCard` eklenir. Geri alma: kayıt varsa reddeder (ödemeler sessizce EFT'ye dönmesin), yoksa kuralı eski haline getirir.
2. Yeni uç `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/tenders/external-card` (aynı grup: terminal erişim süzgeci, hız sınırı; `payments.take`, yeni yetki yok). Gövde `Amount`, `SlipNumber`, `IdempotencyKey`. Kart terminali TANIMLIYSA `409 CARD_TERMINAL_CONFIGURED` (normal kart akışı kalır; ikisi karışmaz; `CARD_TERMINAL_NOT_CONFIGURED` ile simetrik). Fiş numarası boş/çok kısa/çok uzun/geçersiz karakter/kart numarasına benzeyen (13-19 hane ve Luhn geçerli) ise `400` Türkçe cümleyle; aynı hesapta tekrar ise `409 EXTERNAL_CARD_SLIP_REUSED`.
3. Tek işlemde (hesap kilidi, sonra anahtar kilidi; çözülmemiş ödeme ve kalan tutar korumaları EFT ile aynı): `Payment` onaylı oluşur, tahsisat ve `external_card_receipts` satırı yazılır, denetim olayı `payment.external-card-recorded` (kim, fiş numarası, tutar, terminal, hesap; kart numarası alanı yoktur) aynı işlemde eklenir. Nakit hareketi, çekmece açma, nakit sayımına giriş, `card-settlement.approved` olayı OLUŞMAZ. Fiş numarası serbest metin alanlarına (`Note`, ödeme durum geçmişi gerekçesi) yazılmaz. Aynı `IdempotencyKey` aynı gövdeyle tekrar gelirse tek kayıt döner; farklı tutar/fiş/hesapla gelirse `TENDER_IDEMPOTENCY_KEY_REUSED`. Çift tıklama (eşzamanlı aynı anahtar) tek kayıt üretir.
4. `PaymentMethodLookup` yeni `ExternalCard` türünü döndürür (iade, fiş ve ödeme satırları); iade: harici POS kartı ödemesi iade talebinde `ExternalCard` olur, nakit çıkışa dönüşmez (`AwaitingManualRefund`), tamamlamak kart gibi iade fiş numarası ister (iade banka POS'unda yapılır).
5. Sunucu sabitleri: `MinSlipLength = 4`, `MaxSlipLength = 32` (istemci kilidi V1-RMD-1181'de `ClientLimitLockTests` ile bağlanır); fiş normalleştirme mevcut `ManualCardConfirmationService.NormalizeSlip` kuralını yeniden kullanır (genel yapılır).

Rapor satırı V1-RMD-1180, kasiyer ekranı V1-RMD-1181, belgeler V1-RMD-1182 görevindedir.

## Owned surface

- `evidence/V1-RMD-1179/**`
- `database/migrations/V1/V1-RMD-1179/235-external-card-receipts.up.sql`
- `database/migrations/V1/V1-RMD-1179/235-external-card-receipts.down.sql`
- `src/Host/Experience/Billing/ExternalCardTender.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - database/MigrationComposition/order.json
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
  - src/Host/DualScreen/DualScreenApplication.Payments.cs
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - src/Host/Experience/Billing/PaymentMethodLookup.cs
  - src/Host/Experience/Billing/PaymentRefunds.cs
  - src/Modules/Payments/ManualResolution/ManualCardConfirmationService.cs
  - tests/Host/Experience/Billing/RefundMethodHttpTests.cs
  - tests/Host/Experience/PaymentTender/ErrorMessageLanguageTests.cs
  - tests/Host/Experience/PaymentTender/ExternalCardTenderHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon (`cp` + `cmp` ile geri alınır)
2. İki bağımsız qa incelemesi
3. Her kabul koşulu için test: terminal yokken kayıt, terminal varken ret, fiş doğrulaması (boş, boşluk, kısa, uzun, geçersiz karakter, kart numarası benzeri, sınır değerleri), aynı hesapta tekrar ve başka hesapta aynı numara, anahtar tekrarı ve çift tıklama, yetki (403), kalan tutar aşımı, nakit hareketi ve çekmece denetim olayı olmaması, denetim olayı içeriği, iade (nakit çıkışı yok, fiş ister), migration ileri/geri
4. Migration kontrol listesi: manifest, `order.json`, `ManifestTests`, geri alma kanıtı; `ALKAROS.Architecture.*` (ModuleBoundaries, ApiConventions) ve ilgili Host test sınıfları exit code 0

## Out of scope

- Rapor satırı, istemci ekranları ve etiketleri, belgeler (V1-RMD-1180/1181/1182)
- Gerçek kart terminali, mali fiş/ÖKC bağlantısı (bu ödeme türü mali fiş yerine geçmez)
- Fiş numarasının bankada doğrulanması (sistem doğrulayamaz; güven kaydıdır)

## Dependencies

- V1-RMD-1178

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. Migration ileri/geri kanıtı ve plan audit ile consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1180
