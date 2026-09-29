# V1-RMD-453 - Müşteri kaydında vergi kimliği (VKN/TCKN ve vergi dairesi)

- Task ID: V1-RMD-453
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

Semih'in kararı (2026-09-29): faturada alıcının vergi kimliği müşteri kaydından gelir. Müşteri profiline vergi kimlik
türü (VKN ya da TCKN), vergi kimlik numarası ve (VKN için) vergi dairesi eklenir; diğer kişisel verilerle aynı
şifreli zarfta saklanır, eski kayıtlar bu alanlar boş olarak okunmaya devam eder. Kasiyerin Cari Hesaplar ekranından
girilir ve güncellenir. V14-INV-002'nin müşteri anlık görüntüsü bu alanları okur.

## Owned surface

- `plan/v1/remediation/V1-RMD-453-customer-tax-identity.md`
- `evidence/V1-RMD-453/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerData/Profiles/ ve tests/Modules/CustomerData/Profiles/
  (V14-CST-001 sahipliğinde) — yalnız vergi kimliği alanları, doğrulaması ve erişim politikası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/CustomerAccounts/ ve
  tests/Host/Experience/CustomerAccounts/ (V1-RMD-442 sahipliğinde) — yalnız müşteri oluşturma/güncelleme ve listede
  vergi kimliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/customer-accounts/ ve
  tests/E2E/Cashier/specs/30-customer-accounts.spec.js (V1-RMD-443 sahipliğinde) — yalnız vergi kimliği alanları

## In scope

- VKN 10 hane, TCKN 11 hane ve resmi sağlama (checksum) kuralıyla doğrulanır; VKN'de vergi dairesi zorunludur.
- Kasiyer rolü listede vergi numarasını maskeli görür; tam değer yalnız fatura anlık görüntüsünde okunur.
- Anonimleştirilmiş müşteride vergi kimliği de silinir.

## Out of scope

- Fatura oluşturma (V14-INV-002) ve kayıtlı kullanıcı sorgusu (QNB).

## Dependencies

- V14-CST-001
- V1-RMD-443

## Acceptance evidence

- Profil ve HTTP testleri gerçek PostgreSQL üzerinde geçer; geçersiz VKN/TCKN reddedilir; Kasiyer E2E vergi kimliğiyle
  müşteri oluşturur. Çıktılar `evidence/V1-RMD-453/` altındadır.
- Semih'in elle deneyebileceği senaryo: Cari Hesaplar'da şirket müşterisi için VKN ve vergi dairesi girin; yanlış
  haneli numaranın Türkçe gerekçeyle reddedildiğini ve kaydın listede maskeli göründüğünü görün.

## Handoff

- V14-INV-002
