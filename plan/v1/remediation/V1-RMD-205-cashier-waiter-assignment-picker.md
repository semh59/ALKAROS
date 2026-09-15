# V1-RMD-205 - Cashier'da masa açılışında garson seçimi

- Task ID: V1-RMD-205
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-204'ün sunucu tarafını (V1-RMD-111'in çift-katmanlı izniyle korunan
`AssignedWaiterUserId`, öneri ucu `GET .../orders/suggested-waiter`)
Cashier istemcisine bağlar. `cashier-app.js` incelenince görüldü ki Cashier
hiç fiziksel masa seçmiyor — her sipariş sabit bir takma masaya
(`KASA-1`) bağlanıp hemen serbest bırakılıyor (V1-RMD-157). Yani "hangi
masaya" değil, "bu tezgah siparişini kim götürecek" sorusu gerçek boşluk;
sipariş gönderilirken bir garson seçici (öneriyle önceden dolu, kasiyerin
kendisi de bir seçenek) ekleniyor.

## Owned surface

- src/Clients/Cashier/wwwroot/index.html (Sınırlı ek — V1-CUI-005/V1-RMD-051
  sahipliğinde) — sipariş iletim kutusuna yeni garson seçici (`<select>`).
- src/Clients/Cashier/wwwroot/cashier-app.js (Sınırlı ek — aynı sahiplik) —
  oturum açılınca `/orders/staff` + `/orders/suggested-waiter` çekiliyor,
  seçici dolduruluyor (öneri varsayılan seçili); sevkiyat payload'ına
  `assignedWaiterUserId` ekleniyor (kasiyerin kendisi seçiliyse hiç
  gönderilmiyor — sunucunun varsayılan davranışı zaten bu).
- tests/Clients/StaticApps/** (V1-RMD-109 test sahipliğinde) — gerçek
  `index.html`/`cashier-app.js`'i jsdom'da çalıştıran mevcut pakete yeni
  senaryolar. Ayrıca `support/fetchRouter.js`'de gerçek, önceden var olan
  bir kusur bulundu ve düzeltildi: sahte fetch yanıtı hiç `headers` nesnesi
  taşımıyordu, bu yüzden `cashier-app.js`'in `fetchWholeCatalogAsync`'i
  (`response.headers.get('X-Next-Cursor')`) her zaman sessizce patlıyor,
  kataloğu hiç render etmiyordu — bu görevden önce de vardı (git stash ile
  doğrulandı), `waiter-app.test.js`'te hâlâ açık kalan ayrı ve ilgisiz bir
  kusur (bu görevin kapsamı dışında, dokunulmadı).

## In scope

1. Oturum hazır olduğunda paralel iki çağrı: `GET .../orders/staff`,
   `GET .../orders/suggested-waiter`. İkisi de başarısız olursa seçici
   yalnız "Ben" seçeneğiyle kalır — sevkiyat eskisi gibi çalışmaya devam
   eder (bozulan bir şey yok, sadece öneri gösterilmez).
2. Seçici: ilk seçenek her zaman "Ben (kasiyerin adı)" (değer boş —
   `assignedWaiterUserId` hiç gönderilmez, sunucu tarafında zaten
   `actingUserId`'ye düşer); ardından `/staff` listesi. Öneri varsa o
   garson varsayılan seçili gelir, kasiyer isterse değiştirir.
3. Sevkiyatta seçili bir garson varsa `orderPayload.assignedWaiterUserId`
   dolduruluyor.

## Out of scope

- PosTerminal — `/table-draft`'ı hiç çağırmıyor (V1-RMD-204'te doğrulandı).
- WaiterPwa — kendi masasını açan garson zaten kendisi.
- Sunucu tarafı — V1-RMD-204'te tamamlandı, bu görev yalnız istemci.

## Dependencies

- V1-RMD-204

## Acceptance evidence

- `node --check cashier-app.js` → temiz.
- `tests/Clients/StaticApps/**` (gerçek `index.html`/`cashier-app.js`,
  jsdom, üretim koduna dokunmadan) → yeni senaryolar dahil tüm testler
  yeşil: seçici öneriyle önceden dolu geliyor, "Ben" seçiliyken
  `assignedWaiterUserId` hiç gönderilmiyor, başka bir garson seçilince
  gönderiliyor, `/staff`/`/suggested-waiter` başarısız olunca sevkiyat
  yine de çalışıyor.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: Cashier'ı aç, birkaç ürün ekleyip
  "SİPARİŞİ ONAYLA" butonuna gitmeden önce garson seçicisinde sistemin
  önerdiği (en az yüklü) garsonun göründüğünü, farklı bir garson seçip
  gönderdiğinde `GET .../orders/{orderId}` üzerinden ServingUserId'nin o
  garson olduğunu doğrula.

## Handoff

- None
