# V1-RMD-283 - "Kart çekildi" elle çözümü: fiş numarası, iki kişi onayı ve ekstre listesi

- Task ID: V1-RMD-283
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-RMD-264` yalnız "kart ÇEKİLMEDİ" çözümünü getirmişti (para kaydı yaratmaz). Gerçek terminal olmadığı için
onaylanamayan kart ödemesinin gerçekten çekildiği durumu (müşteri slipi gösterdi, kart hesabından düştü) hâlâ
çözemiyorduk; hesap sonsuza dek kilitli kalıyordu. Bu çözüm PARA KAYDI yarattığı için Semih'in onayladığı katı
kurallarla gelir:

1. **Fiş numarası zorunlu ve benzersiz.** 4-32 karakter (harf, rakam, `-`, `/`), büyük harfe çevrilir. Aynı fiş
   iki ödemeyi doğrulayamaz (reddedilmiş bildirimin fişi yeniden kullanılabilir). Veritabanı kısmi benzersiz
   indeksle zorlar.
2. **İki kişi.** Bir yetkili (`reconciliation.manage`) bildirir, FARKLI bir yetkili onaylar. Bildirimi yapan onaylayamaz
   (uygulama `403 SAME_ACTOR` verir; veritabanı `ck_manual_card_four_eyes` ile ikinci savunma hattıdır). Bildirimi
   yapan bildirimini geri çekebilir. Ödeme başına tek bekleyen bildirim.
3. **Yalnız onay para hareket ettirir.** Onay tek işlemde, hesap kilidi altında: ödeme `Approved` olur (kimin,
   hangi fişle), hesaba tahsis edilir, `card-settlement.approved` mali devir olayı kuyruğa yazılır (`manual: true`,
   fiş numarasıyla; gerçek onay ile aynı yol) ve hesap `Paid`, sipariş `Completed` olur.
4. **Denetim ve ekstre listesi.** Her adım denetim olayı yazar (`card-charged-requested/approved/rejected`).
   `GET /api/v1/management/payments/manual-confirmations?status=` (yönetici, `reports.view`) her bildirimi fiş,
   tutar, bildiren ve karar veren ile listeler: banka ekstresi eşleştirmesi bunun üzerinden yapılır.

Migrasyon 143 (`payments.manual_card_confirmations`). Kasa Tahsilat sayfasındaki kilit paneli artık: bildirim yoksa
"Kart çekildi: onaya gönder" (fiş numarası) ve "Kart çekilmedi olarak çöz"; bildirim varsa "Onay bekliyor: fiş …":
bildiren kişi yalnız "Bildirimi geri çek" görür, başka yetkili "Onayla: kart çekildi" / "Reddet".

## Owned surface

- `plan/v1/remediation/V1-RMD-283-manual-card-charged-two-person-confirmation.md`
- `database/migrations/V1/V1-RMD-283/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ManualResolution/ManualCardConfirmation.cs
  (V1-RMD-264 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ManualResolution/IManualCardConfirmationRepository.cs
  (aynı sahiplikte yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ManualResolution/PostgresManualCardConfirmationRepository.cs
  (aynı sahiplikte yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ManualResolution/ManualCardConfirmationService.cs
  (aynı sahiplikte yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ManualResolution/ManualPaymentResolutionExceptions.cs
  (aynı sahiplikte — yalnız altı yeni istisna)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/CardSettlement/CardSettlementModule.cs
  (V13-PAY-004 sahipliğinde kalır — yalnız iki yeni kayıt)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (V13-PUI-001 sahipliğinde kalır — yalnız üç yeni uç nokta, DTO'lar, hata eşlemesi ve özetteki bekleyen bildirim)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Reconciliation/PaymentSettlementEndpoints.cs
  (V1-RMD-265 sahipliğinde — yalnız ekstre listesi uç noktası)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  (V13-PUI-001 sahipliğinde kalır — yalnız kilit panelinin çözüm bölümü)
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  (yalnız 143 girdisi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (yalnız `PhaseBMax` ve doc-comment)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  (yalnız 143 için bayat sabitler)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  (V13-PUI-001 sahipliğinde — 3 yeni test ve yardımcılar)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/12-manual-card-payment-resolution.spec.js
  (V1-RMD-264 ile eklendi — iki kişi senaryosu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/seed.js
  (yalnız ikinci yetkili kullanıcı)

## In scope

1. Migrasyon, depo, iki kişili servis, üç uç nokta, ekstre listesi, denetim, Tahsilat sayfası paneli.
2. Gerçek Postgres HTTP testleri ve iki farklı kullanıcıyla gerçek tarayıcı testi.

## Out of scope

- Ekstreyle otomatik eşleştirme (liste eşleştirme için hazır; içe aktarma ayrı iş).
- Bildirim/onay için mobil bildirim ve bekleme süresi uyarısı.
- Kısmi tutarlı onay (onaylanan tutar, ödemenin kendi tutarıdır).
- Gerçek terminal geldiğinde otomatik mutabakat (bu ekran yedek yol olarak kalır).

## Dependencies

- V1-RMD-264
- V1-RMD-276
- V1-RMD-282
- V1-RMD-265

## Acceptance evidence

- PaymentTender HTTP (UTF8 Postgres 18): 30/30. Yeni: A bildirir → bekleyen bildirim özette, tahsis 0, ödeme `Unknown`;
  A kendi bildirimini onaylamaya çalışınca 403 `SAME_ACTOR`; B onaylayınca ödeme `Approved`, tahsis 40 ₺, hesap `Paid`,
  sipariş `Completed`, iki denetim olayı, `card-settlement.approved` olayı kuyrukta, ikinci onay 409; yetkisiz kasiyer 403,
  geçersiz/boş/kötü karakterli fiş 400, ödeme başına ikinci bekleyen bildirim 409 `CONFIRMATION_PENDING`; aynı fiş (büyük/küçük harf fark
  etmez) ikinci ödemede 409 `SLIP_REUSED`, yanlış hesap yolu 404 ve hiçbir şey değişmez, red sonrası ödeme çözülmemiş kalır ve
  fiş yeniden kullanılabilir.
- **Mutasyon kontrolü:** uygulamadaki dört-göz kuralı kaldırılınca test 403 beklerken hata aldı (veritabanı kısıtı ikinci
  savunma olarak devreye girdi); geri alınınca geçti.
- Cashier E2E (gerçek Host + Chromium, iki gerçek kullanıcı) 33/33: bildirim, bildirenin onay düğmesi yok, ikinci müdür
  onaylar, hesap kapanır, ekstre listesi fişi/tutarı/iki farklı kişiyi taşır. PosTerminal 201/201, vanilla istemci 24/24,
  modül sınır testleri 9/9, `consistency_audit.py`, `project_manifest_tool.py` temiz.

## Handoff

- None
