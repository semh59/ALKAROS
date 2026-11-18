# V1-RMD-1165 - KVKK saklama işi kasa ve masa hareketi serbest metinlerini de temizler

- Task ID: V1-RMD-1165
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih): kalan serbest metin sütunları aynı 5 yıl kuralıyla temizlenir; tutar, durum ve tarih korunur, yalnız metin `[anonymized]` olur. V1-RMD-1164 mutfak ve ödeme metinlerini yaptı; bu görev kalanları kapatır ve runbook'un "Free-text columns outside this job" bölümünü gerçeğe göre bitirir. Seçim `Privacy.RetentionExecution` içinde (sürümlü politika, hold, iş kalemi), yazma `Program.cs` içindeki `KvkkAnonymizationPlans` tablosunda, işaret `[anonymized]`.

1. Yeni `CashFreeText` sınıfı (konu: kasa oturumu). Seçim: oturum durumu `Closed` veya `Reconciled` ve `closed_at` pencereden eski (pencere = `OrderNotes` ile aynı 5 yıl); açık oturum (`Open`, `Counting`, `Closing`) ve bekleyen (`Requested`) hareket talebi olan oturum seçilmez. Metinler: `cash.cash_transactions.notes`, `cash.cash_movement_requests.reason` ve `decision_note`, `cash.cash_counts.notes`, `cash.cash_sessions.override_reason`, `reconciliation_notes`, `difference_explanation`. Tutar, yön, tür, oturum toplamı ve fark sütunları değişmez; kapanış beklenen tutarı yalnız tutar/yön sütunlarından hesaplanır (kodla doğrulanır).
2. Yeni `TableMovementReason` sınıfı (konu: taşıma veya birleştirme kaydı; `transferred_at` / `merged_at` 5 yıldan eski): `table_mgmt.table_transfers.reason`, `table_mgmt.table_merges.reason` ve `unmerge_reason` (birleştirme yalnız `Unmerged` iken).
3. Migration 234: dört kontrol kısıtına iki yeni sınıf eklenir, `assert_policy_complete` 9 sınıf ister, yeni politika sürümü 4 en güncel pencereleri kopyalar ve iki yeni sınıf için `OrderNotes` yılını alır; sürüm 1-3 değişmez. `cash.cash_transactions` ekleme-yalnız bir defterdir: tetikleyici fonksiyonu yalnızca `notes` sütununun tam olarak `[anonymized]` olduğu ve satırın geri kalanının aynı kaldığı UPDATE'e izin verecek biçimde yeniden yazılır (mevcut `recorded_by` boşaltma kolu, DELETE ve tablo boşaltma reddi aynen kalır). Geri alma sürüm 4'ü, kısıtları ve 218'deki fonksiyon gövdesini geri koyar.
4. Hold: dışlanan siparişin (`--exclude-order-ids-file`) taşıma ve birleştirme kayıtları da korunur; kasa oturumu siparişe bağlı olmadığından yalnız `privacy.legal_holds` ile korunur (runbook'a yazılır).
5. Kuru çalıştırma çıktısına iki sayaç eklenir; runbook çıktı örneği ve tablo güncellenir. Müşteri hesap defteri notu ve bilerek temizlenmeyen her sütunun nedeni runbook'a yazılır.

## Owned surface

- `evidence/V1-RMD-1165/**`
- `database/migrations/V1/V1-RMD-1165/234-retention-cash-and-table-classes.up.sql` — yeni sınıflar, politika 4, defter tetikleyicisi.
- `database/migrations/V1/V1-RMD-1165/234-retention-cash-and-table-classes.down.sql` — geri alma.
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

1. Kırmızı test, düzeltme, mutasyon (`cp` + `cmp` ile geri alınır)
2. İki bağımsız qa incelemesi
3. Her sütun için test: süresi dolmuş satırda metin temizlenir, dolmamışta dokunulmaz, tutar/yön/durum/tarih sütunları değişmez, ikinci koşu değişiklik yapmaz; açık oturumdaki eski hareketin, bekleyen talebin, hold altındaki kaydın ve açık birleştirmenin dokunulmazlığı
4. Defter tetikleyicisi: yalnız işaret yazan UPDATE kabul edilir; tutar veya başka sütun değiştiren, başka metin yazan UPDATE, DELETE ve tablo boşaltma reddedilir; migration ileri/geri kanıtı (`RollBackDownTo`); politika 4 beklentileri (1163 ve fatura müşteri testleri) güncellenir
5. `ALKAROS.Architecture.*` ve ModuleBoundaries testleri yeşil

## Out of scope

- `customer_account.account_transactions.note` (nedeni runbook'a yazılır)
- Pencere süresini değiştirmek
- `kvkk-retention --apply` yalnız test PG (55432) üzerinde; canlı veritabanına yazmak

## Dependencies

- V1-RMD-1164

## Acceptance evidence

1. Kırmızı test, yeşil test, mutant listesi
2. Her sütun için süresi dolmuş/dolmamış/değişmeyen sütun/ikinci koşu testleri; açık oturum, bekleyen talep, hold dokunulmazlığı
3. Defter tetikleyicisi testleri ve migration ileri/geri kanıtı
4. İki ardışık bağımsız qa-* incelemesi
5. İlgili test projeleri exit code 0; plan audit ve consistency audit 0 hata 0 uyarı

## Handoff

- None
