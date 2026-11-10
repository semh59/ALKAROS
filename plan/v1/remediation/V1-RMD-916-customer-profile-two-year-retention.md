# V1-RMD-916 - Faturasız müşteri profili son işlemden 2 yıl sonra anonimleşir; faturaya bağlı müşteri 10 yıl

- Task ID: V1-RMD-916
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı (2026-11-10): faturasız müşteri profili son işlemden 2 yıl sonra anonimleşir; faturaya bağlı kayıtlar 10 yıl saklanır. Kodda profil anonimleştirme gerçekten var (V15-KVK-001 saklama seçimi: `CustomerProfile` sınıfı, pencere politikadan, başlangıç son defter hareketi veya fatura; açık bakiyesi veya bekleyen anonimleştirme isteği olan müşteri seçilmez; adım adım anonimleştirme `Program.cs` içindeki plan tablosunda). Ama tek pencere (politika 1: 10 yıl) hem faturasız hem faturalı müşteriye uygulanıyor; faturasız müşteri 10 yıl bekliyor. Karar: iki sınıf. `CustomerProfile` yalnız hiç faturası olmayan müşteriyi seçer ve penceresi 2 yıl olur; yeni `InvoicedCustomerProfile` sınıfı faturası olan müşteriyi seçer ve penceresi 10 yıl kalır (başlangıç aynı kural). Yeni politika sürümü 2, mevcut politikaların yanına migration 225 ile yayımlanır (sürüm 1 değişmez); anonimleştirme planı iki sınıfta aynıdır. KVKK el kitabı bu karara göre düzeltilir ve onaylı V0-CMP-003 envanterindeki 10 yıl satırının bu kararla değiştiği açıkça yazılır. Defter, güncel bakiye ve fatura alıcı verisi dokunulmaz kalır.

## Owned surface

- `evidence/V1-RMD-916/**`
- `database/migrations/V1/V1-RMD-916/225-retention-invoiced-customer-class.up.sql` — yeni sınıf ve politika 2.
- `database/migrations/V1/V1-RMD-916/225-retention-invoiced-customer-class.down.sql` — geri alma.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - database/MigrationComposition/order.json
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
  - src/Modules/Privacy/RetentionExecution/RetentionModels.cs
  - src/Modules/Privacy/RetentionExecution/RetentionSelection.cs
  - src/Host/Program.cs
  - tests/Modules/Privacy/RetentionExecution/RetentionExecutionTests.cs
  - tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs
  - docs/compliance/kvkk-retention-runbook.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Testler: faturasız müşteri 2 yıl sonra, faturalı müşteri 10 yıl sonra seçilir (ikisi de açık bakiye ve bekleyen istek korumasıyla); yeni sınıf için yasal tutma ve politika tamlığı; migration geri alma ve yeniden uygulama
2. Migration, seçim, enum ve anonimleştirme planı değişiklikleri
3. KVKK el kitabı düzeltmesi
4. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Onaylı V0-CMP-003 envanter dosyasının ve güvenlik modülündeki genel imha çizelgesinin (DisposalMatrix, üretimde hiçbir müşteri kaydı bu çizelgeye yazılmıyor) değiştirilmesi; el kitabı notu bu farkı belirtir

## Dependencies

- V1-RMD-915

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
