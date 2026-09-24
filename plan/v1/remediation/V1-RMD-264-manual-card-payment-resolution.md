# V1-RMD-264 - Onaylanamayan kart ödemesi yetkili müdür tarafından "kart çekilmedi" olarak çözülür

- Task ID: V1-RMD-264
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Gerçek kart terminali yok (V13-HUG-001 yasak kalır); BankCard dürüst yer tutucusu
her tahsilatı `RequiresReconciliation` yapar ve hesap, çözecek hiçbir yol olmadan
sonsuza dek kilitli kalıyordu (masa devri/birleştirme ve yeni tahsilat engelli).
Bu görev, `reconciliation.manage` yetkisi olan bir kullanıcının onaylanamayan
kart ödemesini "kart çekilmedi" olarak kapatmasını sağlar. Ödeme `Declined` olur,
hesap kilidi kalkar, HİÇBİR para kaydı (tahsis) oluşmaz: yanlış çağrının en kötü
sonucu müşterinin yeniden ödemesidir. "Kart çekildi (manuel)" çözümü bu görevde
YOKTUR (para kaydı yaratır; fiş no + çift onay + ekstre eşleştirmesi gerektirir,
ayrı bir görev).

Kurallar: yalnız `Unknown`/`ReconciliationRequired` durumundaki ödeme çözülür
(`Pending` uçuştaki bir tahsilattır, dışarıdan ezilmez); gerekçe zorunludur
(en fazla 500 karakter); işlem `bill-settlement:{billId}` danışma kilidi altında
yapılır; ödeme durum geçmişine ve denetim günlüğüne (`payment.manual-resolution.not-charged`)
yazan/gerekçe/zaman ile kaydedilir; ödeme yol dışındaki bir hesaba aitse 404.

## Owned surface

- `plan/v1/remediation/V1-RMD-264-manual-card-payment-resolution.md`
- `src/Modules/Payments/ManualResolution/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/CardSettlement/CardSettlementModule.cs
  (V13-PAY-004 sahipliğinde kalır — yalnız yeni servisin tek satırlık kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (V13-PUI-001 sahipliğinde kalır — yalnız yeni `not-charged` uç noktası ve iki DTO)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  (V13-PUI-001 sahipliğinde kalır — yalnız kilit paneline gerekçe alanı ve çözme düğmesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  (aynı sahiplikte — 5 yeni HTTP testi ve müdür/denetim yardımcıları)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/seed.js
  (V1-CUI-011 sahipliğinde kalır — yalnız `reconciliation.manage` dışında tüm yetkileri olan sınırlı kasiyer)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/paymentHelpers.js
  (yalnız `loginViaApi` için isteğe bağlı kullanıcı adı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/12-manual-card-payment-resolution.spec.js
  (Cashier E2E paketine eklenen yeni dosya)

## In scope

1. `IManualPaymentResolutionService` / `ManualPaymentResolutionService` (durum, gerekçe, eşzamanlılık).
2. `POST .../tenders/unsettled/{paymentId}/not-charged` uç noktası: `reconciliation.manage` zorunlu, denetim olayı.
3. Kasa split-payment sayfasında kilit panelinde gerekçe + "Kart çekilmedi olarak çöz".
4. Host HTTP testleri ve gerçek tarayıcı E2E'si.

## Out of scope

- "Kart çekildi (manuel)" çözümü, fiş numarası, çift onay ve ekstre eşleştirmesi (ayrı görev).
- PosTerminal (React) müdür ekranında aynı işlemin sunulması.
- `Pending` ödemenin elle çözümü.

## Dependencies

- V1-RMD-258
- V13-PUI-001
- V1-RMD-250

## Acceptance evidence

- Host.Experience.PaymentTender (UTF8 Postgres 18): 22/22 (5 yeni: müdür çözer + kilit kalkar + denetim +
  yeniden tahsilat; sade kasiyer 403; boş/boşluk gerekçe 400; ikinci çözüm 409 ve tek denetim olayı;
  başka hesabın ödemesi 404).
- Cashier E2E (gerçek Host + Chromium): 26/26; yeni spec 12 sade kasiyerin Türkçe yetki uyarısı aldığını,
  müdürün gerekçesiz çözemediğini, gerekçeyle çözünce kilidin kalktığını ve tahsis/kalan tutarın değişmediğini sürer.
- Mutasyon kontrolü: uç noktadan `reconciliation.manage` şartı çıkarılınca spec 12 kırıldı; geri alınınca geçti.
- `python tools/plan-audit/plan_audit_tool.py validate` ve `python tools/consistency-audit/consistency_audit.py` çalıştırıldı.

## Handoff

- None
