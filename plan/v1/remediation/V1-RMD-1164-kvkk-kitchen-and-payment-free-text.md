# V1-RMD-1164 - KVKK saklama işi mutfak fişi ve ödeme geçmişi serbest metinlerini de temizler

- Task ID: V1-RMD-1164
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih): kalan serbest metin sütunları da aynı 5 yıl kuralıyla temizlenir; tutar, durum ve tarih korunur, yalnız metin `[anonymized]` olur. `docs/compliance/kvkk-retention-runbook.md` "Free-text columns outside this job" bölümünün ilk yarısı bu görevdedir (ikinci yarısı: kasa ve masa hareketleri, V1-RMD-1165). Mevcut kalıp korunur: seçim `Privacy.RetentionExecution` içinde, yazma `Program.cs` içindeki `KvkkAnonymizationPlans` tablosunda (başka modülün deposuna yazan adımlar kompozisyon kökünde durur), işaret `[anonymized]`.

1. Mutfak (konu: sipariş; mevcut `OrderNotes` sınıfı, aynı pencere ve aynı sipariş durum kuralı; yeni sınıf ve migration yok): `kitchen.kitchen_ticket_items.notes` ve `cancellation_reason`, `kitchen.kitchen_tickets.cancellation_reason`, fişin yazdırılmış kopyaları `kitchen.print_jobs.payload` ile `kitchen.physical_print_deliveries.payload_snapshot` / `reprint_payload` içindeki `NOT:` satırları (yalnız not metni, fişin geri kalanı değişmez), `kitchen.physical_print_deliveries.operator_reason`. Yazdırma işi veya teslimi hâlâ sürüyorsa (`Pending`, `Leased`, `Printing`; `InFlight`, `Unknown`) o satıra dokunulmaz. Seçim bu sütunlarda artık metin kalan siparişi de seçer.
2. Ödeme (konu: adisyon; mevcut `BillFreeText` sınıfı): `payments.payment_status_history.reason`, `payments.payment_reversals.reason`, `payments.manual_card_confirmations.request_note` ve `decision_note` (yalnız `Approved`/`Rejected`). Ödeme `payments.bill_id` ile adisyona bağlanır. Bekleyen manuel kart onayı veya sonuçlanmamış ödemesi (`Initiated`, `Pending`, `Unknown`, `ReconciliationRequired`) olan adisyon, bunların metni işlem için gerekli olduğundan henüz seçilmez (açık iade talebi kuralıyla aynı).
3. Migration 233: `payment_reversals` tablosu ekleme-yalnız bir defterdir (tetikleyici her UPDATE'i reddeder). Tetikleyici fonksiyonu, yalnızca `reason` sütununun tam olarak `[anonymized]` olduğu ve satırın geri kalanının aynı kaldığı UPDATE'e izin verecek biçimde yeniden yazılır; INSERT tutar denetimi ve DELETE reddi aynen kalır. Geri alma 182'deki fonksiyon gövdesini aynen geri koyar.
4. Okuyanlar kodla doğrulanır (hiçbiri değeri karşılaştırmaz veya dallanmaz; kasa/rapor tutar toplamları `amount` okur), kuru çalıştırma çıktısı değişmez (sayaçlar mevcut sınıf adlarında kalır).
5. Runbook: sütun tablosu ve "Free-text columns outside this job" bölümü gerçeğe göre güncellenir; yazdırma kopyaları, 1163'ün tamamlanmış iş kalemi uyarısı ve bu görevde bilerek temizlenmeyenlerin nedeni yazılır.

## Owned surface

- `evidence/V1-RMD-1164/**`
- `database/migrations/V1/V1-RMD-1164/233-payment-reversal-retention-scrub.up.sql` — tetikleyici fonksiyonu.
- `database/migrations/V1/V1-RMD-1164/233-payment-reversal-retention-scrub.down.sql` — geri alma.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - database/MigrationComposition/order.json
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
  - src/Modules/Privacy/RetentionExecution/RetentionSelection.cs
  - src/Host/Program.cs
  - tests/Modules/Privacy/RetentionExecution/RetentionExecutionTests.cs
  - tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs
  - docs/compliance/kvkk-retention-runbook.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon (`cp` + `cmp` ile geri alınır)
2. İki bağımsız qa incelemesi
3. Her sütun için test: süresi dolmuş satırda metin temizlenir, dolmamışta dokunulmaz, tutar/durum/tarih sütunları değişmez, ikinci koşu değişiklik yapmaz; sürmekte olan yazdırma işi, bekleyen manuel kart onayı ve sonuçlanmamış ödeme dokunulmazlığı; hold altındaki sipariş/adisyon dokunulmazlığı
4. Defter tetikleyicisi: yalnız işaret yazan UPDATE kabul edilir; tutar veya başka sütun değiştiren, başka metin yazan UPDATE ile DELETE reddedilir; migration ileri/geri kanıtı (`RollBackDownTo`)
5. `ALKAROS.Architecture.*` ve ModuleBoundaries testleri yeşil

## Out of scope

- Kasa, kasa hareketi ve masa taşıma/birleştirme metinleri (V1-RMD-1165)
- `customer_account.account_transactions.note` (müşteri hesap defteri; yasal saklama, müşteri pencereleri ayrı karar; nedeni runbook'a yazılır)
- Pencere süresini değiştirmek (5 yıl `OrderNotes` kararıdır)
- `kvkk-retention --apply` yalnız test PG (55432) üzerinde; canlı veritabanına yazmak

## Dependencies

- V1-RMD-1163

## Acceptance evidence

1. Kırmızı test, yeşil test, mutant listesi
2. Her sütun için süresi dolmuş/dolmamış/değişmeyen sütun/ikinci koşu testleri; sürmekte olan iş, bekleyen onay, açık ödeme ve hold dokunulmazlığı
3. Defter tetikleyicisi testleri ve migration ileri/geri kanıtı
4. İki ardışık bağımsız qa-* incelemesi
5. İlgili test projeleri exit code 0; plan audit ve consistency audit 0 hata 0 uyarı

## Handoff

- V1-RMD-1165
