# V1-RMD-241 - Add an idempotency key to manual cash-drawer movements

- Task ID: V1-RMD-241
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`POST .../cash-sessions/{id}/cash-movements` (`V13-CSH-004`) always inserts
a brand-new `CashTransaction` with a fresh `Guid.NewGuid()` — unlike its
sibling `cash-tender` endpoint, it takes no idempotency key at all. A
bağımsız denetim ajanı (2026-09-18, tüm proje kod denetimi, Host
composition alanı) bunu tespit etti: bir ağ tekrarı (retry) veya çok hızlı
art arda iki istek aynı nakit giriş/çıkışını iki kez kaydedebilir. Bu görev
`cash-tender`'ın zaten test edilmiş `IdempotencyKey` desenini
`cash-movements`'a taşır.

## Owned surface

- `database/migrations/V1/V1-RMD-241/**`
- `evidence/V1-RMD-241/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/TransactionLedger/CashTransaction.cs,
  ICashTransactionLedgerRepository.cs, PostgresCashTransactionLedgerRepository.cs
  (V13-CSH-002 sahipliğinde kalır) — `CashTransaction`'a sondan eklenen
  isteğe bağlı `IdempotencyKey` (mevcut hiçbir çağıran değişmez, hepsi
  `null` bırakır); repository'ye `idempotency_key` kolonu ve yeni bir
  `GetBySessionAndIdempotencyKeyAsync` metodu eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs
  (V13-CSH-004 sahipliğinde kalır) — yalnız `/cash-movements` endpoint'i,
  `cash-tender`'ın zaten kullandığı "önce kontrol et, sonra ekle, çakışmada
  var olanı getir" desenini uygular; `RecordCashMovementRequestV1`'e
  `cash-tender`'ın `CashTenderRequestV1`'i ile aynı şekilde zorunlu bir
  `IdempotencyKey` alanı eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  (V13-PUI-002 sahipliğinde kalır) — `submitCashMovement` her çağrıda
  gerçek bir `crypto.randomUUID()` üretip `IdempotencyKey` olarak gönderir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  (V13-CSH-004 sahipliğinde kalır) — yeni bir tekrar (retry) testi eklenir,
  mevcut testler `IdempotencyKey` alanı eklenerek güncellenir (artık
  zorunlu).
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Cash/TenderHandler/ALKAROS.Cash.TenderHandler.Tests.csproj
  (V13-CSH-003 sahipliğinde kalır) — bu projenin kendi sabit migration
  fixture listesine 130'un fixture bağlantısı eklendi (`RecordAsync`'in
  artık gerçekten yazdığı `idempotency_key` kolonu olmadan bu proje
  derlenmiyordu değil ama çalışma zamanında `42703` ile patlıyordu — gerçek
  test koşusuyla yakalandı).

## In scope

- `cash_transactions` tablosuna isteğe bağlı `idempotency_key` kolonu +
  `(cash_session_id, idempotency_key)` üzerinde yalnız `idempotency_key
  IS NOT NULL` iken geçerli bir UNIQUE index.
- Aynı `(cashSessionId, idempotencyKey)` ile ikinci bir `/cash-movements`
  isteği, ikinci bir satır oluşturmaz — ilk kaydı olduğu gibi döner.

## Out of scope

- `cash-tender` endpoint'inin kendisi (zaten doğru).
- Opening/Sale/Refund gibi diğer `CashTransaction` üretim yolları — hepsi
  `IdempotencyKey: null` bırakır, davranışları değişmez.

## Dependencies

- V13-CSH-002
- V13-CSH-004

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: aynı `IdempotencyKey`
  ile `/cash-movements`'a iki kez POST edildiğinde, ikinci istek de 2xx
  döner ama `GetBySessionIdAsync` yalnız BİR CashIn/CashOut satırı içerir
  (önceden iki satır oluşurdu).
- Migration boş bir veritabanında ileri/geri (up/down) denenir.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (Cash.TransactionLedger + CashSession HTTP testleri) → yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
