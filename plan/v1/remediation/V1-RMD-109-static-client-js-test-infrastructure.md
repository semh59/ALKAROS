# V1-RMD-109 - Cashier/WaiterPwa vanilla JS test infrastructure

- Task ID: V1-RMD-109
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("JS test altyapısından başla", önceki oturumda önerilen
madde listesinden, 2026-09-06), `src/Clients/Cashier/wwwroot/cashier-app.js`
ve `src/Clients/WaiterPwa/wwwroot/waiter-app.js`'in — bugünkü iki Critical
bulgunun (sipariş verisi kaybı, siparişin mutfağa hiç gitmemesi) tam olarak
içinde yaşadığı, hiç otomatik testi olmayan iki dosyanın — artık gerçek,
üretim koduna dokunmadan çalışan bir test paketi var.

## Owned surface

- `tests/Clients/StaticApps/**` (yeni)
- Bu görev, başka bir task'in owned surface alanını değiştirmedi —
  `src/Clients/Cashier/wwwroot/**` ve `src/Clients/WaiterPwa/wwwroot/**`
  yalnız okundu (gerçek `index.html` + üretim script'i doğrudan yüklenip
  çalıştırılıyor), hiçbir üretim dosyası yazılmadı.

## In scope

- `tests/Clients/StaticApps/`: bağımsız bir npm paketi (vitest + jsdom),
  ana pnpm workspace'ten (PosTerminal) ayrı, kendi `package-lock.json`'ı ile.
- `support/loadApp.js`: bir istemcinin gerçek `index.html` gövdesini
  (elle bakımı yapılan, gerçek markup'tan sürüklenebilecek bir iskelet
  yerine) jsdom belgesine yükleyip gerçek `wwwroot/*.js` dosyasını
  `file://` URL + cache-bust sorgusuyla dinamik `import()` eden ortak
  yardımcı — her testin taze bir kapanış-durumlu (`state`) modül örneği
  alması için.
- `support/fetchRouter.js`: URL/method eşleşmesine göre yanıt üreten
  minimal `fetch` sahtesi.
- `cashier-app.test.js` (3 test): sipariş gönderimi table-draft'ın
  ardından submit-draft'ı doğru sırayla çağırıyor mu (bugünkü Critical
  bulgunun regresyon kanıtı); çift-tıklama koruması ikinci bir istek
  göndermiyor mu; sunucu reddi ham HTTP kodu göstermiyor mu.
- `waiter-app.test.js` (4 test): aynı üç senaryo + giriş ekranında ağ
  hatasının ham İngilizce mesaj yerine Türkçe metin göstermesi.
- Revert-and-confirm: "table-draft'tan sonra submit-draft" testi, submit-
  draft çağrısı geçici olarak kaldırılıp yeniden eklenerek gerçekten
  bugünkü bulguyu yakaladığı doğrulandı (3 yerine 4 çağrı bekleniyor,
  zaman aşımıyla başarısız oldu).

## Out of scope

- Üretim JS dosyalarında herhangi bir değişiklik — bu paket yalnız
  gözlemliyor, hiçbir üretim davranışını değiştirmedi.
- PosTerminal (React/TS) test altyapısı — zaten vitest/jsdom ile mevcut.
- Garson-masa servis atama modeli — ayrı bir göreve bırakıldı.

## Dependencies

- V1-RMD-108

## Acceptance evidence

- `cd tests/Clients/StaticApps && npm install && npx vitest run`:
  7/7 test geçti (2 test dosyası).
- Revert-and-confirm: `cashier-app.js`'teki submit-draft çağrısı geçici
  olarak kaldırıldığında ilgili iki test beklenen şekilde başarısız oldu
  ("expected ... to be called 4 times, but got 3 times"); dosya geri
  yüklenip tam süit yeniden yeşil oldu (`git diff` temiz).
- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata (bu görev .NET
  koduna dokunmadı, mevcut derlemeyi bozmadığı doğrulandı).
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.
- Semih'in elle deneyebileceği senaryo: `cd tests/Clients/StaticApps &&
  npx vitest run` — Cashier ve WaiterPwa'nın gerçek üretim script'leri
  gerçek DOM'larıyla çalıştırılıp mutfağa gönderim akışı uçtan uca
  doğrulanıyor.

## Handoff

- V1-GOV-095
