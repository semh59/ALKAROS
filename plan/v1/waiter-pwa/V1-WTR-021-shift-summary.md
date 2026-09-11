# V1-WTR-021 - Yalnızca kendine görünen vardiya özeti

- Task ID: V1-WTR-021
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler" bölümünden
9. madde (Katman C): Toast/Square/Lightspeed'in hepsinde bulunan bir
"vardiya kapanışı" (shift review/closeout) ekranının küçük bir sürümü —
garsonun yalnızca kendi o günkü satış toplamını, kullandığı ikram
bütçesini ve bahşiş havuzu payını görmesi. Hiçbir yönetici görünümü yok,
başka bir garsonun rakamları asla görünmez.

**Semih'in araştırma sonrası kararları (2026-09-11):**
1. Bahşiş bölüşümü: **eşit havuz** — bugün toplanan tüm bahşiş, bugün en
   az bir sipariş alan garson sayısına eşit bölünür (kim hangi siparişe
   bahşiş girdiği önemli değil).
2. Vardiya özetine bahşiş payı dahil edilsin (V1-WTR-020'nin bahşiş
   altyapısına bağımlı).

**"Vardiya" = UTC takvim günü**, yeni bir vardiya/punch-clock kavramı
icat edilmedi — `PersonalCompBudgetEscalationResolver`'ın (V1-WTR-012)
zaten kurduğu aynı sıfırlama sınırı yeniden kullanıldı.

**Canlı okuma modeli, batch rapor değil:** `reporting.
waiter_performance_summaries` tablosu zaten vardı ama bu bir gün-sonu
snapshot tablosu (hiç bahşiş alanı da yok) — bir vardiyanın ORTASINDAKİ
garson için canlı sayılar vermez. Bunun yerine `orders.orders`
(serving_user_id, bugünkü satış toplamı), `identity.authorization_grants`
(bugünkü ikram onayları) ve `billing.bill_adjustments` (bugünkü Tip
satırları) üzerinde doğrudan ham SQL — `TableManagementStore.
CurrentOrderTotalSql`'in kurduğu "yeni tablo değil okuma modeli"
deseninin bir başka uygulaması.

**V1-WTR-020 ile birlikte tek commit'te kapatıldı:** bu görev
V1-WTR-020'nin (gönüllü bahşiş) hemen ardından geldi;
`AVoluntaryTipTodayAppearsInTheServingWaitersShiftSummary` testi her iki
uç noktayı da uçtan uca çalıştırıyor (önce bahşiş kaydediliyor, sonra
vardiya özetinde göründüğü doğrulanıyor) — iki ayrı, birbirini tekrar
eden test dosyası yerine tek, gerçek bir entegrasyon testi.
`BillingSplitHttpTests`'in kendi `_database`'ini (tam migration dizinini
uygulayan) yeniden kullanan, yalnızca bu test için ayrı bir
`StartAsyncWithOrders()` host kompozisyonuyla eklendi — paylaşılan
`StartAsync()`'i (16 var olan testin kullandığı) değiştirmeden.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-021-shift-summary.md` (yeni)
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementContracts.cs,
    OrderManagementStore.cs, OrderManagementEndpoints.cs (Host
    sahipliğinde) — `MyShiftSummaryV1`, `GetMyShiftSummaryAsync`,
    `GET /orders/my-shift-summary`.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js, waiter-app.css,
    index.html (V1-WTR-010 sahipliğinde) — profil menüsüne "Vardiya
    özetim" seçeneği, `openShiftSummarySheet`, `.shift-summary-*`
    sınıfları.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (Billing test
    sahipliğinde) — uçtan uca test (bkz. Not).

## Out of scope

- Nakit/kart ayrımı, gün sonu kapanış tutanağı — Cash modu V1.2'ye
  ertelendi.
- Bahşiş payının gerçek zamanlı (canlı, saniyede güncellenen) gösterimi —
  sheet açıldığında bir kez sorgulanıyor, WaiterPwa'nın diğer "sheet"
  desenleriyle tutarlı (ör. profil menüsünün kendisi de statik).

## Dependencies

- V1-WTR-012
- V1-WTR-020

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz; `waiter-app.css` parantez dengesi
  259/259.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test
  tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj`
  → 17/17 (V1-WTR-020'nin 3 testi dahil, artı bu görevin uçtan uca testi:
  bugün kaydedilen 40 TL'lik bahşiş, o siparişi alan tek garsonun vardiya
  özetinde tam payı — 40 TL — olarak görünüyor; satış toplamı > 0; ikram
  kullanımı 0).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
