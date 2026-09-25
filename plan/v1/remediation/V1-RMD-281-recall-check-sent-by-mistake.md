# V1-RMD-281 - Yanlışlıkla kasaya gönderilen hesap masaya geri alınır

- Task ID: V1-RMD-281
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

"Hesabı kasaya gönder" tek dokunuştur, dolayısıyla yanlışlıkla basılacaktır. Kilitli tasarım
(`V1-ORD-006`) hesabı masadan koparıp masayı hemen yeni müşteriye açıyor; geri dönüş yolu YOKTU ve garsonun
bu akışının tarayıcıda hiç testi de yoktu. Müşteri masada kalıp yeni sipariş verirse kasada aynı masadan iki
hesap birikiyor ve hata kasiyerin yüküne dönüyordu.

`POST /orders/{orderId}/recall-from-cashier` (aynı `orders.create` yetkisi) hesabı masaya geri bağlar:
masa tekrar `Occupied`, sipariş masanın hesabı, kasada açılmış ama içinde tahsilat olmayan hesaplar Billing modülünün deposu
üzerinden iptal edilir (Host deposu başka alanın şemasına yazmaz; kasiyer hayalet hesap toplamaz, yeniden gönderimde
taze hesap kurulur; istek yarıda kalırsa tekrarı tamamlar), denetim olayı `check.recalled-from-cashier` yazılır.

Kapılar (hepsi Türkçe, hepsi 409/404): tahsilat başlamışsa (tahsis ya da çözülmemiş ödeme ya da `Paid` hesap)
`CHECK_HAS_PAYMENT`; masada zaten daha yeni bir hesap varsa `TABLE_HAS_OPEN_CHECK` (kilitli tasarım işaretçinin
sessizce değiştirilmesine hiç izin vermez; garsona söylenir); hesap kasa kuyruğunda değilse ya da başka masanındaysa
`CHECK_NOT_RECALLABLE`. Aynı isteği tekrarlamak zararsızdır (`AlreadyAttached`). Geri alınan hesap yeniden gönderilebilir.

İstemciler:

- Garson PWA: gönderim başarı bildirimi artık **10 saniyelik "Geri al"** düğmesi taşır (5 sn yerine; bir el kayması
  fark edilip düzeltilebilsin); geri alınamazsa sunucunun Türkçe nedeni gösterilir.
- Kasiyer "Bekleyen hesaplar": tahsilat başlamamış her satırda "Yanlışlıkla gönderildi: masaya geri gönder";
  kısmen ödenmiş satırda seçenek yoktur.

## Owned surface

- `plan/v1/remediation/V1-RMD-281-recall-check-sent-by-mistake.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/08-send-to-cashier-undo.spec.js
  (V1-WTR-026 sahipliğindeki Garson E2E paketine eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/CashierHandoffStore.cs
  (V1-ORD-006 sahipliğinde kalır — yalnız geri alma yöntemi, üç istisna ve kuyrukta `tableId`)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementContracts.cs
  (aynı sahiplikte — yalnız geri alma sözleşmeleri ve `TableId` alanı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs
  (Orders Host sahiplerinde kalır — yalnız geri alma uç noktası)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/sheets/pending-orders.js
  (V1-WTR sahipliğinde kalır — yalnız geri al bildirimi ve çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/toast.js
  (aynı sahiplikte — yalnız geri al bildiriminin süresi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/pending-checks/pendingChecksApi.ts
  (V1-RMD-280 sahipliğinde — geri gönderme çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx
  (aynı sahiplikte — geri gönder düğmesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx
  (aynı sahiplikte — 2 yeni test)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/pending-checks/pending-checks.css
  (aynı sahiplikte — düğme stili)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs
  (V1-ORD-006 sahipliğinde — 4 yeni test)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftTestDatabase.cs
  (yalnız hesap durumu yardımcısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/lib/seed.js
  (V1-RMD-260 sahipliğinde — yalnız üç dokunulmamış `SND-n` masası)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/14-till-queue-of-sent-checks.spec.js
  (V1-RMD-280 ile eklendi — geri gönderme senaryosu)

## In scope

1. Sunucu geri alma, iki istemci, gerçek Postgres ve gerçek tarayıcı testleri.

## Out of scope

- Masada yeni hesap varken iki hesabı BİRLEŞTİRMEK: bu sürüm reddedip nedenini söyler; birleştirme (hesap bölme ile
  etkileşimi olan kalem taşıma) ayrı bir karar/görevdir.
- Tahsilat başlamış hesabı geri almak (para hesaba işlenmiş; bilerek kapalı).
- Gönderirken ek onay (zaten "Kasaya gönder" onay sayfası var).

## Dependencies

- V1-ORD-006
- V1-RMD-279
- V1-RMD-280

## Acceptance evidence

- Host.Experience.Orders.TableDraft (UTF8 Postgres 18): 84/84. Yeni: gönderilen hesap geri alınır (masa `Occupied`,
  işaretçi hesapta, açık hesap `Cancelled`, kuyruktan çıktı), tekrar `AlreadyAttached`, yeniden gönderilebilir;
  tahsisi olan hesap 409 `CHECK_HAS_PAYMENT`, hesap `Open` kalır, masa boş kalır; masada daha yeni hesap varken
  409 `TABLE_HAS_OPEN_CHECK` ve işaretçi yeni hesapta kalır; kuyrukta olmayan/başka masanın hesabı 404, oturumsuz 401.
- WaiterPwa E2E (gerçek Host + Chromium) 43/43. Yeni: gönder → "Geri al" → masa yeniden dolu ve sunucuda hesap
  masaya bağlı; masada yeni hesap açılmışken "Geri al" Türkçe "yeni bir hesap açık" uyarısı verir.
- Cashier E2E 32/32. Yeni: kasadan "masaya geri gönder" hesabı kuyruktan çıkarır, masa yeniden `Occupied` ve o hesaba
  bağlı; yeniden gönderip 60 ₺ tahsilattan sonra seçenek kaybolur ve sunucu 409 `CHECK_HAS_PAYMENT` verir.
- PosTerminal vitest tümü geçti (yeni 2), vanilla istemci testleri geçti, `consistency_audit.py` ve
  `plan_audit_tool.py validate` temiz.

## Handoff

- None
