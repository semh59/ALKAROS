# V13-CSH-004 - Compose CashSession and cash tender over HTTP

- Task ID: V13-CSH-004
- Status: Done
- Assignee: Codex
- Work type: implementation
- Surface state: Planned

## Goal

`ICashSessionLifecycleService` (V13-CSH-001) ve `ICashTenderHandler`
(V13-CSH-003) bugün hiçbir HTTP endpoint'i olmayan, yalnızca izole domain
modülleri olarak var. `PO:2026-09-18` (Semih, bu oturumda doğrudan onay —
V13-PUI-002'ye başlamadan önce, gerçek bir backend'e bağlanmayan bir
kasiyer arayüzü inşa edilemeyeceği bulgusu üzerine): bu iki servisi
terminal-scoped Host'a (`DualScreen`) bağlamak, böylece V13-PUI-002'nin
(ve gelecekte V13-PAY-003'ün tam kompozisyonunun) çağıracağı gerçek bir
yüzey olsun.

## Owned surface

- `src/Host/DualScreen/DualScreenApplication.CashSession.cs` (yeni —
  `DualScreenApplication.Screensaver.cs`'in kendi dosya deseniyle aynı:
  ayrı bir partial-class dosyası, `MapApi`'nin çağırdığı tek bir
  `MapCashSessionApi` extension metodu).
- `tests/Host/Experience/CashSession/**` (yeni test projesi).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (çok sayıda geçmiş dalga görevinin sahipliğinde kalır) — iki nokta:
  (1) `Build`'in `Map*Api()` çağrı zincirine, Screensaver'ın kendi
  `app.MapCustomerDisplayScreensaverApi();` çağrısıyla aynı desende bir
  `app.MapCashSessionApi();` eklenir; (2) `WriteErrorAsync`'in
  exception→HTTP eşleme switch'ine yeni Cash/CashTender exception tipleri
  için satırlar eklenir. Mevcut hiçbir endpoint/eşleme/kayıt değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs
  — yalnız `ModuleRegistry.DefaultCatalog`'a şu ana kadar hiç
  kaydedilmemiş dört modül eklenir: `CashSessionLifecycleModule`,
  `CashTransactionLedgerModule`, `PaymentAllocationPersistenceModule`,
  `CashTenderHandlerModule` (`PaymentAggregateModule` zaten orada); bu,
  V13-PAY-003'ün "tam V1.3 kompozisyonu" işinin küçük bir alt kümesi —
  yalnız BUGÜN çalışan Cash yüzeyi için.
- `evidence/V13-CSH-004/**`

## In scope

- Terminal-scoped auth: mevcut kasiyer cookie/session doğrulamasıyla aynı
  desen (`RequireCashierSessionAsync`), manager-only aksiyonlar için
  `bills.discount`/`catalog.manage` düzeyinde bir izin gerektirmez —
  CashSession zaten kendi `CashierUserId`/rol modeliyle çalışır
  (V1-CSH-001'in kendi izin sınırı, V1-IAM-002).
- Uç noktalar (terminal-scoped, `/api/v1/terminals/{terminalId}/cash-sessions/**`):
  - `GET .../cash-sessions/suggested-opening-balance`
  - `GET .../cash-sessions/active` (açık/sayım/kapanış aşamasındaki
    oturum varsa onu, yoksa 404 döner — V13-PUI-002'nin "ikinci açık
    oturum engellenir" ekranı bunu okur)
  - `POST .../cash-sessions` (Open)
  - `POST .../cash-sessions/{id}/start-count`
  - `POST .../cash-sessions/{id}/counts` (RecordCount)
  - `POST .../cash-sessions/{id}/close` (expectedCash,
    `ICashTransactionLedgerRepository.ComputeExpectedCashAsync`'ten
    hesaplanır — V13-CSH-001'in kendi Goal'ının "V13-CSH-002 gelince
    caller onu toplasın" notu artık gerçekleşebilir)
  - `POST .../cash-sessions/{id}/reconcile`
  - `POST .../cash-sessions/{id}/cash-tender` (`ICashTenderHandler`,
    `AmountDue`/`TenderedAmount`/`IdempotencyKey`/hedef `billId` body'de)
  - `GET .../cash-sessions/{id}/expected-cash` (Ek, 2026-09-18:
    V13-PUI-002'nin Fark Teyidi ekranı `CashSessionSnapshot.ExpectedCash`'i
    okuyordu, ama bu alan yalnız `/close`'un kendi yazma yolunda tazelenir —
    kapatmadan önce hâlâ açılıştaki eski değeri taşıyordu, ekranda yanlış
    "Beklenen" tutarı gösteriyordu; bu, `/close`'un zaten kullandığı
    `ComputeExpectedCashAsync`'in salt-okunur bir önizlemesi)
  - `POST .../cash-sessions/{id}/cash-movements` (Ek, 2026-09-18:
    V13-PUI-002'nin tasarım geçişinde "Nakit Giriş/Çıkış" ekranı ortaya
    çıktı ama bu ekranın çağıracağı bir uç nokta hiç yoktu — satış dışı
    manuel kasa hareketi, `ICashTransactionLedgerRepository.RecordAsync`
    üzerinden doğrudan `CashIn`/`CashOut` tipinde bir `CashTransaction`
    kaydeder; yalnız oturum Open iken kabul edilir)
- Her mutasyon uç noktası gerçek row-version/idempotency hatalarını (409
  Conflict, mevcut `DualScreenApplication`'ın hata haritalama deseniyle)
  doğru HTTP koduna çevirir.

## Out of scope

- V13-PUI-002'nin kendi UI'ı (ayrı görev, bu task'ın Handoff'u).
- Kart/yemek kartı tender uç noktaları (V13-PAY-004/MCD-004, dış
  blocker'a bağlı).
- Mutabakat kontrol paneli ve fiscal kapanış geçidi, sırasıyla V13-REC-001
  ve V13-FSC-002'nin kendi kapsamında kalır.

## Dependencies

- V13-CSH-001
- V13-CSH-002
- V13-CSH-003
- V1-IAM-002

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testleri: açma, sayım,
  kapatma, fark teyidi, mutabakat, cash tender — hepsi başarı ve en az
  bir ret yolunu (ikinci açık oturum, kapalı oturumda tender, eksik
  tutar, stale row-version) kapsar.
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V13-PUI-002
