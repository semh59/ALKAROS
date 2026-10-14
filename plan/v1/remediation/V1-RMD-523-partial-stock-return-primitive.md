# V1-RMD-523 - Stoğa kısmi geri dönüş (Return hareketi)

- Task ID: V1-RMD-523
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

Doğrulanan durum: stok tüketimi kalem başına ürün ve eklenti kayıtlarıyla `Consumption` hareketi olarak yazılır; `CreateReversal` yalnız tüm miktarı ve en fazla bir kez geri alır. PO kararı: iadede adet seçilebilir ve kasiyer stoğa geri dönüşü seçer. Bu görev bir sipariş kaleminin tüketim hareketlerinden iade edilen adede orantılı `Return` hareketi (kaynak: Order + sipariş kalemi) yazar ve toplam geri dönüşün tüketimi aşmasını engeller.

## Owned surface

- `plan/v1/remediation/V1-RMD-523-partial-stock-return-primitive.md`
- `evidence/V1-RMD-523/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/MovementReversal/IStockMovementReversalService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/MovementReversal/StockMovementReversalService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/MovementReversal/PartialStockReturnTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Girdi: sipariş kalemi kimliği, iade edilen adet, toplam adet; her tüketim hareketi için miktar × adet/toplam (4 ondalık, yuvarlama farkı son iadede kapanır) kadar giriş hareketi, aynı depoda, bakiye aynı işlemde artar.
- Aynı kalemde toplam geri dönüş tüketilen miktarı aşamaz; tekrar çağrı idempotent anahtarla korunur.
- Gerçek Postgres testi.

## Out of scope

- Sipariş akışı ve arayüz; para iadesi.

## Dependencies

- V1-RMD-424

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-523/` altındadır.

## Handoff

- None
