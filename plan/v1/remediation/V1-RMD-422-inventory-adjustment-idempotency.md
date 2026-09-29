# V1-RMD-422 - Elle stok düzeltmesinde işlem kimliğinin gerçekten uygulanması

- Task ID: V1-RMD-422
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-398 stok/reçete denetimi G-07 (Düşük, bugün): `InventoryAdjustmentRequest.IdempotencyKey` kabul ediliyor ama
hiç okunmuyordu; aynı anahtarla gönderilen iki düzeltme stoğu iki kez değiştiriyordu (denetimde 27 yerine 26). Bu
servisi bugün hiçbir HTTP ucu çağırmıyor; kabul edilip yok sayılan bir alan sahte bir güvence verir (AGENTS.md
yer tutucu yasağı).

Bu görev: işlem kimliği verilen düzeltme, anahtardan türetilen deterministik bir kaynak referansıyla deftere yazılır;
kayıt aynı işlem içinde anahtar başına bir advisory kilit altında aranır. Aynı anahtarın tekrarı ilk düzeltmenin
sonucunu döner ve stok ikinci kez değişmez; aynı anahtar farklı bir düzeltme (başka kalem, lokasyon, yön ya da
miktar) için kullanılırsa reddedilir. Anahtarsız düzeltmelerin davranışı değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-422-inventory-adjustment-idempotency.md`
- `evidence/V1-RMD-422/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs ve
  src/Modules/Inventory/ManualAdjustments/AdjustmentModels.cs (V11-INV-005 sahipliğinde) — yalnız işlem kimliği
  kontrolü ve sonuçtaki `WasReplayed` alanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/ManualAdjustments/ManualAdjustmentDatabaseTests.cs
  (V11-INV-005 sahipliğinde) — yeni testler

## In scope

- Aynı anahtarla sıralı ve eşzamanlı tekrarlar tek hareket yazar.
- Aynı anahtar farklı düzeltme için → mevcut ama bugüne dek hiç atılmayan `DuplicateAdjustmentException`.

## Out of scope

- Düzeltme için HTTP ucu (bugün yok).
- Defter şeması (migration gerekmez).

## Dependencies

- V1-RMD-421

## Acceptance evidence

- `ALKAROS.Inventory.ManualAdjustments.Tests` 10/10 ve `ALKAROS.Host` derlemesi (gerçek PostgreSQL 18, Release, 0 uyarı /
  0 hata; `evidence/V1-RMD-422/tests.log`). Anahtarsız düzeltme testleri değişmeden geçer.
- Yeni test `RetriesOfAKeyedAdjustmentMoveStockOnceAndAReusedKeyIsRefused`: 30 kg'dan aynı anahtarla 1 kg azaltma; ilk
  istek uygulanır, sıralı tekrar ve 5 eşzamanlı tekrar aynı hareketi döner (`WasReplayed`), bakiye 29, defterde iki
  hareket; aynı anahtar 2 kg için → `DuplicateAdjustmentException`, bakiye 29 kalır, defterden yeniden hesap 29.
  Servis değişikliği geri alınınca kırmızı (tekrar yeniden uygulanıyordu; `evidence/V1-RMD-422/red-without-fix.log`).
- V1-RMD-398 probe'u S08 düzeltilmiş kopyada geçer (`evidence/V1-RMD-422/stock-flow-probe-s08.log`).
- Semih'in elle deneyebileceği senaryo: bugün bu servisi çağıran bir ekran yok; ileride eklenecek elle stok düzeltme
  ekranında, ağ tekrarında ya da çift tıklamada stok iki kez değişmez.

## Handoff

- None
