# V1-WTR-047 - JS refactor adım 11/?: sheets/void-comp.js ve sheets/help-request.js modülleri çıkarma

- Task ID: V1-WTR-047
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı, planın kendi listesindeki kalan sheet
modüllerinden ikisi: `openVoidSheet`/`confirmVoid`/`openVoidSentSheet`/
`confirmVoidSent`/`openCompSheet`/`confirmComp` (+ `VOID_REASONS`/
`COMP_REASONS` sözlükleri, `pendingVoidKeys`/`pendingCompKeys`
haritaları) `js/sheets/void-comp.js`'e; `openHelpRequestSheet`/
`confirmHelpRequest` (+ `HELP_REQUEST_TYPES` sözlüğü)
`js/sheets/help-request.js`'e taşındı.

`loadOrder` de bu adımda `js/sheets/bill.js`'e taşındı (planın kendi
listesinde ayrıca sayılmıyordu) — bu üç sheet'in üçü de aynı "mutasyon
sonrası siparişi yeniden yükle" desenini paylaşıyor, `activeItems`'ın
zaten aynı gerekçeyle `bill.js`'e taşınmış olmasıyla aynı mantık.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/sheets/void-comp.js` (yeni).
- `src/Clients/WaiterPwa/wwwroot/js/sheets/help-request.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/js/sheets/bill.js — `loadOrder`
    eklendi (V1-WTR-045'in sahipliğinde kalır, bu görev yalnız ekliyor).
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — sekiz fonksiyon + üç
    sözlük + iki `Map` silindi, `import` listesine üç satır eklendi.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v13'ten v14'e
    yükseltildi; `ASSETS_TO_CACHE`'e iki yeni dosya eklendi.

## Out of scope

- Kalan sheet modülleri (transfer, pending-orders, failed-orders,
  profile) ve en riskli ikisi (`offline-queue.js`, `push.js`) — ayrı
  görevler.

## Dependencies

- V1-WTR-046

## Acceptance evidence

- `node --check` üç yeni/değişen dosyanın da (`js/sheets/void-comp.js`,
  `js/sheets/help-request.js`, `js/sheets/bill.js`) ve `waiter-app.js`'in
  sözdizimsel olarak geçerli olduğunu doğruladı. Bu adımda `grep`'le her
  `import` edilen ismin gerçekten çağrıldığı ayrıca tek tek kontrol
  edildi (bir önceki adımın eksik-import hatasından çıkarılan ders) —
  bu kontrol `KITCHEN_STATE`'in artık `waiter-app.js`'te kullanılmadığını
  bulup import'tan çıkardı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **3/3 çalıştırmada 18/18
  temiz** (~43s/çalıştırma).
- `grep -n "pendingVoidKeys\|pendingCompKeys\|HELP_REQUEST_TYPES\|
  VOID_REASONS\|COMP_REASONS"` `waiter-app.js`'te → yalnız bir yorum
  satırı eşleşiyor (gerçek tanım yok — taşındı, kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
