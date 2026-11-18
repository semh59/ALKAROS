# V1-RMD-1163 - KVKK saklama işi fatura, iade ve durum geçmişi serbest metinlerini de temizler

- Task ID: V1-RMD-1163
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

`docs/compliance/kvkk-retention-runbook.md` "Known gap" bölümü: saklama işi şu serbest metin sütunlarını temizlemiyor (Privacy altında bunlara referans yok): `billing.bill_items.notes` (V1-RMD-1161/1162 ile kasada kalem notu artık gerçekten dolu), `billing.bill_adjustments.notes`, `billing.bills.cancellation_reason`, `payments.refund_requests.reason` ve `decision_note`, `orders.order_status_history.reason` (V1-RMD-724). Mevcut kalıp korunur: seçim `Privacy.RetentionExecution` içinde (sürümlü politika, hold, iş kalemi), yazma `Program.cs` içindeki `KvkkAnonymizationPlans` tablosunda (başka modülün deposuna yazan adımlar kompozisyon kökünde durur), işaret `[anonymized]`, satır ve tutar/durum sütunları dokunulmaz.

1. `orders.order_status_history.reason` mevcut `OrderNotes` sınıfına eklenir (konu: sipariş; pencere: aynı 5 yıl, aynı terminal durum kuralı). Seçim, yalnız bu sütunda artık metin kalan siparişi de seçer; yeni adım `orders.order_status_history`.
2. Yeni `BillFreeText` sınıfı (konu: adisyon). Seçim: durum `Paid` veya `Cancelled` ve `created_at` pencereden eski (pencere = `OrderNotes` ile aynı 5 yıl); `bill_items.notes`, `bill_adjustments.notes`, `bills.cancellation_reason` veya kapanmış (`Completed`/`Rejected`) iade talebinin `reason`/`decision_note` alanında `[anonymized]` olmayan metin kalmışsa adisyon seçilir. Açık iade talebi (`Requested`, `AwaitingManualRefund`) işlenmek için gerekli olduğundan dokunulmaz. Adımlar: `billing.bill_items`, `billing.bill_adjustments`, `billing.bills`, `payments.refund_requests`; yalnız metin `[anonymized]` olur (sıfır/`NULL` metin yazılmaz, zorunlu `reason` sütunu dolu kalır), `row_version` artar, tutar/durum/tarih sütunları değişmez.
3. Migration 232: dört kontrol kısıtına (`retention_rules`, `legal_holds`, `retention_work_items`, `anonymization_jobs`) `BillFreeText` eklenir, `assert_policy_complete` 7 sınıf ister, yeni politika sürümü 3 en güncel pencereleri kopyalar ve `BillFreeText` için `OrderNotes` yılını alır. Sürüm 1 ve 2 değişmez; geri alma sürüm 3'ü ve kısıtları eski haline getirir.
4. Kuru çalıştırma çıktısı ve uygulama yeni sınıfı sayar; çıktı satırına `bill_texts=<n>` eklenir.
5. Runbook: tablo ve "Known gap" bölümü gerçeğe göre güncellenir (kapananlar çıkar, kalan varsa nedeni yazılır); pencere ve sınıf bilgisi eklenir.

## Owned surface

- `evidence/V1-RMD-1163/**`
- `database/migrations/V1/V1-RMD-1163/232-retention-bill-free-text-class.up.sql` — yeni sınıf ve politika 3.
- `database/migrations/V1/V1-RMD-1163/232-retention-bill-free-text-class.down.sql` — geri alma.
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
3. Her sütun için test: süresi dolmuş satırda metin temizlenir, dolmamışta dokunulmaz, tutar/durum sütunları değişmez, ikinci koşu değişiklik yapmaz
4. Migration ileri/geri kanıtı; `ALKAROS.Architecture.*` ve ModuleBoundaries testleri yeşil

## Out of scope

- Başka serbest metin sütunları (kasa hareketi, manuel kart onayı notları vb.); bu görev yalnız yukarıdaki sütunlarla sınırlıdır
- Pencere süresini değiştirmek (5 yıl `OrderNotes` kararıdır)
- `kvkk-retention --apply` veya audit-disposal çalıştırmak, canlı veritabanına yazmak

## Dependencies

- V1-RMD-916
- V1-RMD-1162

## Acceptance evidence

1. Kırmızı test, yeşil test, mutant listesi
2. Her sütun için süresi dolmuş/dolmamış/değişmeyen sütun/ikinci koşu testleri; açık iade talebinin dokunulmazlığı; hold altındaki adisyonun dokunulmazlığı
3. Migration ileri/geri kanıtı (`RollBackDownTo`)
4. İki ardışık bağımsız qa-* incelemesi
5. İlgili test projeleri exit code 0; plan audit ve consistency audit 0 hata 0 uyarı

## Handoff

- None
