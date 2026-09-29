# V1-RMD-440 - Müşteri başına kredi limiti ve vadesi geçmiş borçta cariye yazmanın reddi

- Task ID: V1-RMD-440
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

V1-RMD-436 cari hesaba borç yazmayı, kredi limiti kavramı olmadığı için tamamen kapattı. Semih'in kararı
(2026-09-29): müşteri başına kredi limiti; limit aşılırsa ya da vadesi geçmiş borç varsa kesin ret.

Bu görev:

- Yeni tablo `customer_account.credit_terms` (migration 164): müşteri başına kredi limiti (TL, 0 veya üstü) ve
  isteğe bağlı vade (gün, 1-365). Kaydı olmayan müşterinin limiti 0'dır (veresiye yok).
- `CreditTermsCreditPolicy` (`NoCreditLimitDefinedPolicy`'nin yerine): mevcut bakiye + yeni borç limiti aşarsa ret;
  vade tanımlıysa, ödemeler en eski borçtan başlayarak düşüldüğünde vadesinden eski ödenmemiş borç kalıyorsa ret.
  Gerekçeler Türkçe.
- `AccountChargeHandler` kredi kontrolünü müşteri başına danışma kilidi (advisory lock) altında, transaction içinde
  yapar; aynı müşteriye eşzamanlı iki borç yazma limiti birlikte aşamaz.
- Yönetici ucu `GET`/`PUT /api/v1/management/customers/{customerId}/credit-terms`: `settings.manage` izni (yönetici,
  yükseltme yolu yok; ayar yönetimiyle aynı yönetici oturumu). Anonimleştirilmiş ya da olmayan müşteri 404.

## Owned surface

- `plan/v1/remediation/V1-RMD-440-per-customer-credit-limit-and-overdue-refusal.md`
- `evidence/V1-RMD-440/**`
- `src/Modules/CustomerAccounts/CreditTerms/**`
- `database/migrations/V1/V1-RMD-440/**`
- `src/Host/Experience/CustomerCredit/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/BillCharges/ICustomerCreditPolicy.cs,
  src/Modules/CustomerAccounts/BillCharges/AccountChargeHandler.cs ve
  src/Modules/CustomerAccounts/BillCharges/CustomerAccountsBillChargesModule.cs (V14-ACC-003 sahipliğinde) — yalnız
  politika değişimi, kilit altında kredi kontrolü ve kayıtlar
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/CustomerAccounts/BillCharges/AccountChargeHandlerTests.cs,
  tests/Modules/CustomerAccounts/BillCharges/CreditTermsCreditPolicyTests.cs ve
  tests/Modules/CustomerAccounts/BillCharges/ALKAROS.CustomerAccounts.BillCharges.Tests.csproj (V14-ACC-003 test
  projesi) — yeni testler ve migration 164 fikstürü
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Settings/CustomerCreditTermsHttpTests.cs
  (V1-RMD-246 test projesinde yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs (ana Host kompozisyonu) —
  yalnız yeni deneyimin kaydı ve eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs —
  yalnız migration 164
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-272
  sahipliğinde) — yalnız kaldırılan politikanın satırı

## In scope

- Kredi koşulları tablosu, deposu, politikası, kilit altında kontrol ve yönetici ucu.

## Out of scope

- Kredi limitini gösteren/düzenleyen istemci ekranı (bugün cari hesap ekranı yok).
- "Hesaba yaz" tahsilat uç noktası.

## Dependencies

- V1-RMD-439

## Acceptance evidence

- Modül testleri (gerçek PostgreSQL 18): kaydı olmayan müşteri reddedilir; limit içindeki borç yazılır; limiti
  aşan borç reddedilir ve hiçbir kayıt oluşmaz; vadesi geçmiş ödenmemiş borç varken ret, o borç ödenince kabul;
  aynı müşteriye eşzamanlı iki borç limiti birlikte aşamaz. `evidence/V1-RMD-440/tests.log`.
- HTTP testleri: yönetici limiti yazar ve okur; oturumsuz 401, izinsiz 403, olmayan müşteri 404, negatif limit 400.
- Düzeltme geri alınınca (her borcu onaylayan politika) limit ve vade testleri kırmızı
  (`evidence/V1-RMD-440/red-without-fix.log`).
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla `PUT /api/v1/management/customers/{id}/credit-terms`
  gövdesi `{"creditLimit": 500, "paymentTermDays": 30}`; bu müşteriye 500 TL'yi aşan cari borç ya da 30 günden eski
  ödenmemiş borcu varken yeni cari borç reddedilir.

## Handoff

- None
