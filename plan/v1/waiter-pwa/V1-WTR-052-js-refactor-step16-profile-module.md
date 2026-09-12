# V1-WTR-052 - JS refactor adım 16/?: sheets/profile.js modülü çıkarma

- Task ID: V1-WTR-052
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı, planın kendi listesindeki son sheet modülü:
`openProfileSheet`/`openShiftSummarySheet` `js/sheets/profile.js`'e
taşındı. `openProfileSheet`'in `refreshPushState`'e bağımlılığı
V1-WTR-051'in (`push.js`) tamamlanmış olmasıyla çözülebildi.

Bu adımla planın 2.4'ündeki TÜM ekran/sheet modülleri tamamlandı.
Kalan tek şey `offline-queue.js` — planın kendi uyarısına göre en
riskli, son adım.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/sheets/profile.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — iki fonksiyon
    silindi, `import` listesine bir satır eklendi. Bu adımda ayrıca
    artık kullanılmayan dört import daha bulunup kaldırıldı:
    `escapeHtml`, `formatMoney` (`js/util.js`'ten), `featureEnabled`
    (`js/features.js`'ten), `openOptions` (`js/options-sheet.js`'ten) —
    bu isimlerin tümü, taşınan son kalan çağıranlarıyla (`openProfileSheet`/
    `openShiftSummarySheet`) birlikte gitmişti; her importun DIŞARIDAN
    (yalnız kendi tanımının içinden değil) gerçekten çağrıldığı tek tek
    doğrulandı.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v18'den v19'a
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/sheets/profile.js` eklendi.

## Out of scope

- `offline-queue.js` — planın kendi listesindeki son, en riskli modül,
  ayrı bir görev.

## Dependencies

- V1-WTR-045
- V1-WTR-051

## Acceptance evidence

- `node --check` her iki dosyanın (`js/sheets/profile.js`,
  `waiter-app.js`) da sözdizimsel olarak geçerli olduğunu doğruladı.
  Bu adımda `waiter-app.js`'in TÜM import listesi (yalnız bu adımın
  eklediği değil) tek tek `grep`'le tekrar taranarak dört fazladan
  kullanılmayan import bulundu ve temizlendi.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma).
- `grep -n "^  function openProfileSheet\|^  async function
  openShiftSummarySheet"` `waiter-app.js`'te → 0 eşleşme (taşındı,
  kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
