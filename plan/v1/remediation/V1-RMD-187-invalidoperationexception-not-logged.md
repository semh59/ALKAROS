# V1-RMD-187 - `InvalidOperationException`'ın 409'a düşen hâli hiç loglanmıyordu

- Task ID: V1-RMD-187
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin bulgusunu
kapatır: `OrderManagementExceptionFilter`, yalnızca 500+ durum
koduna eşlenen istisnaları `LogRequestFailure` ile logluyordu.
`InvalidOperationException` sabit olarak 409 (`CONCURRENCY_CONFLICT`)'e
eşlendiği için bu eşiğin altında kalıyor ve hiç loglanmıyordu.

Ama `InvalidOperationException`, bu kod tabanında pek çok farklı sebep
için fırlatılıyor — sipariş bulunamadı, "gönderilecek kalem yok",
V1-RMD-182'nin kendi "Cancelled'dan kurs ateşlenemez"i, gerçek bir
concurrency çatışması — hepsi çağırana aynı genel concurrency-çatışması
mesajıyla dönüyordu. Gerçek bir concurrency-olmayan hata burada sunucu
tarafında sıfır iz bırakıyordu; üretimde teşhis etmenin hiçbir yolu
yoktu.

## Owned surface

- `plan/v1/remediation/V1-RMD-187-invalidoperationexception-not-logged.md` (yeni)
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (Host
    sahipliğinde) — `OrderManagementExceptionFilter`'a, `Warning`
    seviyesinde ayrı bir `LogInvalidOperation` logu eklendi (409'a
    eşlenen genel olağan yol olduğu için `Error` değil — bir gerçek
    concurrency çatışması alarm gerektirmiyor, ama teşhis edilebilir
    olması gerekiyor).

## Out of scope

- `InvalidOperationException`'ın HTTP eşlemesini/mesajını
  değiştirmek (her farklı neden için kendi özel exception türünü
  tanımlamak): bu, bu görevin kapsamının çok üzerinde bir refactor —
  bulgu yalnızca "loglanmıyor" idi, mesajın kendisi değil.

## Dependencies

- None

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test
  tests/Host/Experience/Orders/TableDraft/*.csproj` → **67/67 yeşil**
  (regresyon yok — HTTP yanıtının durum kodu/mesajı değişmedi, yalnızca
  ek bir log satırı eklendi).
- Bu değişikliği doğrudan sınayan ayrı bir log-yakalama testi eklenmedi:
  bu test projelerinde önceden var olan bir `ILoggerProvider`
  yakalama altyapısı yok, ve düşük öncelikli bir gözlemlenebilirlik
  düzeltmesi için yeni bir test altyapısı kurmak bu görevin kapsamının
  ötesinde — kod incelemesiyle (LoggerMessage.Define çağrısının doğru
  EventId/seviye/şablonla tanımlandığı, filtrenin doğru koşulda
  çağırdığı) doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
