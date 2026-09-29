# V1-RMD-453 - Müşteri kaydında vergi kimliği (VKN/TCKN ve vergi dairesi)

- Task ID: V1-RMD-453
- Status: Done
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

- Profil testleri (gerçek PostgreSQL 18, 60/60): VKN sağlama hanesi, TCKN 10. ve 11. hane kuralları, uzunluk, VKN'de
  vergi dairesi zorunluluğu; yönetici tam numarayı, kasiyer yalnız son üç haneyi görür, diğer roller hiçbir şey görmez;
  numara veritabanında düz metin olarak yer almaz; güncelleme değiştirir ve kaldırır; anonimleştirme vergi kimliğini de
  siler; maskeli değer geri yazılamaz; eski kayıtlar vergi kimliği olmadan okunur. Sağlama kontrolü devre dışı
  bırakılınca üç test kırmızı (`evidence/V1-RMD-453/mutation-vkn-checksum.log`).
- HTTP testleri (19/19): VKN ile oluşturulan müşteri yanıtta ve listede maskeli; sekiz hatalı girdi Türkçe gerekçeyle
  400 ve kayıt oluşmaz; `PUT /api/v1/terminals/{terminalId}/customers/{customerId}/tax-identity` vergi kimliğini
  koyar, değiştirir, kaldırır ve diğer alanları korur; bilinmeyen müşteri 404; yetkisiz 403. Anonimleştirme, hesaba
  yazma ve uç yetkilendirme paketleri yeşil (`evidence/V1-RMD-453/dotnet-test.log`).
- Kasiyer E2E (3/3): hatalı VKN Türkçe gerekçeyle reddedilir, doğrusu ile müşteri eklenir, kayıt listede ve ekstrede
  `VKN *******890 · Kadıköy V.D.` olarak görünür, tam numara sayfada yoktur; TCKN'ye geçiş kaydedilir
  (`evidence/V1-RMD-453/e2e-customer-accounts.log`).
- Semih'in elle deneyebileceği senaryo: Kasa → Cari Hesaplar'da yeni müşteri için "VKN (şirket)" seçip 1234567891 ve
  bir vergi dairesi girin; "Vergi kimlik numarası geçersiz; rakamları kontrol edin." uyarısını görün. 1234567890 ile
  ekleyin; listede `VKN *******890` görünür. Müşteriyi açıp "Fatura için vergi kimliği" alanından TCKN 10000000146
  girip "Vergi kimliğini kaydet"e basın.

## Handoff

- V14-INV-002
