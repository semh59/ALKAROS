# V1-RMD-179 - Masa geçişinde bayat sipariş yarışı

- Task ID: V1-RMD-179
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin (backend,
frontend, API endpoint, boundary/DI, database schema) en yüksek öncelikli
bulgusunu kapatır: bir garson masa A'ya dokunup hemen ardından masa B'ye
geçtiğinde, iki `openTable()` çağrısı üst üste biner. A'nın kendi
`GET /orders/table/{id}` yanıtı — hata değil, sadece sıradan ağ gecikmesi
yüzünden — B'ninkinden SONRA gelirse, eski kod bunu koşulsuzca
`state.order`'a yazıyordu: garson B'nin ekranındayken B'nin başlığının
altında A'nın kalemlerini, A'nın tutarını görüyordu.

Kanıt: `tests/E2E/WaiterPwa/specs/` altına geçici bir repro spec'i
(`zz-temp-stale-order-race.spec.js`, commit edilmedi) eklenip
`page.route()` ile masa E2E-1'in kendi sipariş yanıtı 1200ms geciktirildi,
sonra E2E-1 → E2E-2 hızlı geçişi simüle edildi. Düzeltme YOKKEN test 1/1
FAILED oldu — `#billBody` gerçekten E2E-2 başlığı altında E2E-1'in
gönderilmiş "E2E Köfte ₺280,00" siparişini gösterdi (Gönderildi, Mutfakta,
İkram, İptal iste durumları dahil). Düzeltme VARKEN aynı test 1/1 passed.
(`git stash` ile düzeltme geçici olarak kaldırılıp test yeniden çalıştırılarak
doğrulandı — testin kendisi anlamsız/her zaman geçen bir test değil.)

## Owned surface

- `plan/v1/remediation/V1-RMD-179-stale-order-race-on-table-switch.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/js/sheets/bill.js (V1-WTR-045
    sahipliğinde) — `loadOrder(tableId)`'a, kendi yanıtını yazmadan önce
    `state.table` hâlâ aynı masa mı diye bir tazelik kontrolü eklendi.
  - src/Clients/WaiterPwa/wwwroot/js/screens/tables.js (V1-WTR-043
    sahipliğinde) — `openTable(tableId, ...)`'a, `await loadOrder(tableId)`
    döndükten hemen sonra aynı tazelik kontrolü eklendi; artık masa
    değişmişse `renderBill()`, ekran geçişi ve hand-off notu adımlarının
    hiçbiri eski masa için çalışmıyor.

## Out of scope

- V1-RMD-180..185 ve sonrası: audit'in kalan bulguları — ayrı görevler.
- `loadTableSeats()`'in kendi olası benzer yarışı: koltuk listesi salt
  bilgilendirici bir seçenek listesi, para/sipariş içeriği taşımıyor;
  aynı ciddiyette bir bulgu değil, bu göreve dahil edilmedi.

## Dependencies

- V1-WTR-043
- V1-WTR-045

## Acceptance evidence

- Geçici repro spec (`zz-temp-stale-order-race.spec.js`, commit edilmedi):
  - Düzeltme YOKKEN (`git stash` ile geçici kaldırılarak): 1/1 FAILED,
    gerçek DOM kanıtıyla (`#billBody` E2E-1'in siparişini E2E-2 başlığı
    altında gösterdi).
  - Düzeltme VARKEN: 1/1 passed.
  - Test dosyası doğrulama sonrası silindi; kalıcı kapsamın bir parçası
    değil.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  npx playwright test` (`tests/E2E/WaiterPwa`), gerçek Postgres + gerçek
  Host binary + gerçek Chrome: **18/18 yeşil, 3 ardışık çalıştırmada**
  (bu değişiklik `openTable`/`loadOrder`'a dokunduğu için, her spec'in
  dolaylı olarak sınadığı bu akış özellikle tekrar tekrar doğrulandı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
