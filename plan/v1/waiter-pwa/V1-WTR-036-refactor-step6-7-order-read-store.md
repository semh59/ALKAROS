# V1-WTR-036 - Refactor adım 6+7/7: OrderReadStore ve tamamlanış

- Task ID: V1-WTR-036
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatının devamı ("Başla" → "Durum değerlendirmesi yap ve
devam et", 2026-09-12) —
`docs/engineering/garson-refactor-plan.md`'in Bölüm 1.4'ündeki 7 adımın
**son ikisi, tek görevde birleştirildi**: adım 5 tamamlandıktan sonra
`OrderManagementStore.cs`'te kalan tek şey, planın "OrderReadStore"
diye adlandırdığı 4 metot (`GetOrderByIdAsync`,
`GetActiveOrderByTableIdAsync`, `TransferServingUserAsync`,
`GetPendingOrdersAsync`) ve `InvalidTransferTargetException`'dı —
başka hiçbir şey kalmamıştı. Bu yüzden adım 6 (yeni sınıfı çıkar) ve
adım 7'nin geri kalanı (eski dosyayı sil, endpoint'leri yeniden bağla),
tek bir yeniden adlandırma işlemiydi: içerik aynen `OrderReadStore.cs`
adlı yeni dosyaya taşındı, eski `OrderManagementStore.cs` silindi.

**Refactor artık tamamen bitti.** `OrderManagementStore.cs` (1285 satır,
tek dosya) yerine 6 odaklı dosya var:

```text
OrderDtoAssembler.cs            288 satır
TableDraft/TableDraftService.cs 684 satır (en büyük, kendi klasöründe)
OrderSubmissionCoordinator.cs   150 satır
CashierHandoffStore.cs          108 satır
OrderReadStore.cs               133 satır
ShiftSummaryStore.cs             93 satır
                        Toplam: 1456 satır (yorumlar/using'ler tekrarlandığı
                        için orijinalden biraz fazla — beklenen, davranış
                        aynı, dosya sayısı arttı)
```

## Owned surface

- `src/Host/Experience/Orders/OrderReadStore.cs` (yeni).
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-ORD-00x
    ailesinin sahipliğinde) — **silindi**.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (aynı
    sahiplik) — kalan 4 `OrderManagementStore store` parametresi
    (`GET /pending`, `GET /table/{tableId}`, `GET /{orderId}`,
    `POST /transfer-server`) `OrderReadStore store`'a çevrildi; DI kaydı
    `OrderManagementStore` → `OrderReadStore`.
  - src/Host/Experience/Orders/TableDraft/TableDraftService.cs,
    OrderSubmissionCoordinator.cs (ilgili sahiplik) — bu görevden ÖNCE
    (adım 4/5'te) yazılan, artık var olmayan `OrderManagementStore`'a
    işaret eden 3 yorum satırı `OrderReadStore`'a düzeltildi (kod
    değişikliği yok, yalnız doğruluk).

## Out of scope

- Bu görevin ortaya çıkardığı yeni bir teknik borç değil — refactor'ın
  KENDİSİ artık tamamlandı, kapsam dışı bırakılan hiçbir şey yok.

## Dependencies

- V1-WTR-035

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/Experience/Orders/{TableDraft,Confirmation,Comp,Void,VoidSent}`
    → 66/19/14/5/14, hepsi yeşil.
  - `tests/Host/Experience/NfcOrdering` → 17/17.
  - `tests/Host/Experience/Billing` → 18/18.
  - `tests/Host/MigrationComposition` (tam paket, DI kompozisyonu
    dahil) → 135/135 — `OrderReadStore`'un DI'da sorunsuz çözüldüğünün
    ve `OrderManagementStore`'un silinmesinin hiçbir yeri bozmadığının
    kanıtı.
- `tests/E2E/WaiterPwa` (gerçek Chrome + gerçek Postgres + gerçek Host)
  → **3 kez art arda çalıştırıldı, 18/18 her seferinde** — refactor'ın
  SON adımı olduğu için tek koşumla yetinilmedi.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal (yol boyunca kendi yazdığım 2 yorum
  satırındaki Türkçe karakter — "Bölüm 1.3" — de düzeltildi). Kalan tek
  ihlal, `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
