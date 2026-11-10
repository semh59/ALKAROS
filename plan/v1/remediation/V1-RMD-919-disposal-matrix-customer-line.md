# V1-RMD-919 - DisposalMatrix müşteri satırı 2y/10y kararıyla hizalanır

- Task ID: V1-RMD-919
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Doğrulandı: `DataCategory.CustomerPii` için üretimde hiçbir kod `security.retention_subjects` kaydı oluşturmuyor (`InsertAsync` çağıranı yok; testlerde de `CustomerPii` geçmiyor). CustomerProfile.cs içindeki kullanılmayan `RetentionYears = 10` sabiti ve "retention 10 years" yorumu da aynı çelişkiyi taşır (kullanan yok), silinir. Müşteri verisinin saklama süresi artık Privacy modülünde (`CustomerProfile` 2 yıl, `InvoicedCustomerProfile` 10 yıl). `DisposalMatrix.RetentionPeriods` içindeki 10 yıllık müşteri satırı bu kararla çelişen yanıltıcı bir ikinci kaynaktır. Kategori değeri veritabanı CHECK kısıtında (migration 137) ve 9 kategorili envanterde durduğundan silinmez; süresi `null` yapılır (otomatik süre taraması müşteri verisine dokunmaz, sahibi Privacy modülüdür) ve yorumda nedeni yazılır; el kitabı notu buna göre düzeltilir.

## Owned surface

- `evidence/V1-RMD-919/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Security/DataProtectionRetention/DisposalMatrix.cs
  - src/Modules/CustomerData/Profiles/CustomerProfile.cs
  - tests/Modules/Security/DataProtectionRetention/RetentionCoverageVerifierTests.cs
  - docs/compliance/kvkk-retention-runbook.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: müşteri kategorisinin otomatik saklama süresi yok (Privacy sınıfları sahibi)
2. Matris satırının hizalanması, el kitabı notu
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Genel `retention_subjects` süpürmesinin (üretimde kayıt üreticisi olmayan) tümüyle kaldırılması

## Dependencies

- V1-RMD-918

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
