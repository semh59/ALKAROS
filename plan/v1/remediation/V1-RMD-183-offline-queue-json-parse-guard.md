# V1-RMD-183 - `offlineQueue`/`failedOrders`'ın korumasız `JSON.parse`'ı

- Task ID: V1-RMD-183
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin frontend
boyutundaki bulgusunu kapatır: `state.js`'in `offlineQueue`/
`failedOrders`'ı kendi localStorage anahtarlarından çıplak bir
`JSON.parse` ile okuyordu — aynı dosyadaki `loadDraftsByTable()`'ın
zaten taşıdığı (V1-RMD-170) `try/catch` korumasının aksine. Bu modül
uygulamadaki neredeyse her şeyden önce import edildiği için, burada
fırlayan bir `SyntaxError` (elle bir düzenleme, eski bir uygulama
sürümünden kalan bozuk şema, aynı origin'e yazan başka bir eklenti) tüm
uygulamayı import zamanında çökertiyordu — çevrimdışı kuyruk değil,
her ekran.

Kanıt: `tests/E2E/WaiterPwa/specs/` altına geçici bir repro spec'i
(`zz-temp-corrupt-localstorage.spec.js`, commit edilmedi) eklendi:
`page.addInitScript()` ile `alkaros_waiter_offline_queue` anahtarına
geçersiz JSON yazıldı, sonra gerçek `login()` akışı denendi. Düzeltme
YOKKEN test, `#loginUsername`'ın hiç görünür olmadığını (giriş ekranı
bile açılmadı) kanıtladı. Düzeltme VARKEN aynı test 1/1 passed.

## Owned surface

- `plan/v1/remediation/V1-RMD-183-offline-queue-json-parse-guard.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/js/state.js (V1-WTR-038 sahipliğinde) —
    `loadDraftsByTable()`'ın hemen altına aynı desenin genel bir hâli
    olan `loadJsonArray(key)` eklendi; `offlineQueue`/`failedOrders`
    artık ikisi de bunu kullanıyor.

## Out of scope

- Yok — iki alanın da aynı kök nedenle aynı korumaya kavuşması.

## Dependencies

- V1-WTR-038
- V1-RMD-170

## Acceptance evidence

- Geçici repro spec (`zz-temp-corrupt-localstorage.spec.js`, commit
  edilmedi):
  - Düzeltme YOKKEN (`git stash` ile geçici kaldırılarak): FAILED,
    gerçek kanıtla (`#loginUsername` hiç görünür olmadı — giriş formu
    hiç açılmadı, tüm modül import zamanında çöktü).
  - Düzeltme VARKEN: 1/1 passed.
  - Test dosyası doğrulama sonrası silindi; kalıcı kapsamın bir parçası
    değil.
- `node --check js/state.js` → temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  npx playwright test` (`tests/E2E/WaiterPwa`) → 18/18 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
