# V1-RMD-085 - Reporting business-day close transactional integrity

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi

## Problem

`OperationalReportService.CloseBusinessDayAsync` iş gününü kapatıyor, sonra garson
özetlerini, sonra baskı hata özetlerini ayrı ayrı `IOperationalReportRepository`
çağrılarıyla yazıyordu. Her çağrı kendi bağlantısını açıyordu; ortak transaction
yoktu. Kapanıştan sonraki bir hata iş gününü kapalı ama özetleri eksik bırakıyordu
(V1-RMD-084 bulgu #1).

## Change

- `IOperationalReportRepository.CloseBusinessDayWithSummariesAsync(...)` eklendi:
  tek bağlantı, tek transaction içinde
  1. `pg_advisory_xact_lock` ile iş günü kilidi (eşzamanlı açma/kapama yarışına karşı),
  2. `SELECT ... FOR UPDATE` ile mevcut satırın okunması ve durum kontrolü,
  3. `UPDATE ... SET status = 'Closed' ... WHERE status = 'Open'` (etkilenen satır sayısı doğrulanır),
  4. garson özetlerinin `INSERT`'i,
  5. baskı hata özetlerinin `INSERT`'i,
  6. yazılan özetlerin aynı transaction içinde geri okunması,
  7. `COMMIT`.
  Herhangi bir adımda hata olursa `await using` transaction dispose edilir ve
  tüm yazılar geri alınır.
- `OperationalReportService.CloseBusinessDayAsync` artık yalnızca bu tek metodu
  çağırıyor; ayrı ayrı yazan `CloseBusinessDayAsync` + `RecordWaiterSummaryAsync`
  döngüsü + `RecordPrintErrorSummaryAsync` döngüsü + ayrı geri-okuma zinciri
  kaldırıldı. Kamu imzası (opsiyonel `waiterSummaries`/`printSummaries`
  parametreleri) değişmedi; `null` argümanlar boş listeye çevriliyor.
- Eski `RecordWaiterSummaryAsync` / `RecordPrintErrorSummaryAsync` repo metotları
  imzada kaldı (repository seviyesinde geçerli operasyonlar) ama kapanış akışı
  onları artık kullanmıyor.

## Test

`tests/Modules/Reporting/V1Operations/PostgresOperationalReportRepositoryTests.cs`
içine `CloseBusinessDayWithSummariesRollsBackWholeCloseWhenSummaryWriteFails`
eklendi: iki garson özeti aynı `SummaryId` ile gönderilir; ikinci `INSERT` birincil
anahtar ihlali (`PostgresException`, SqlState 23505) fırlatır. Test şunları doğrular:

- çağrı `PostgresException` fırlatır,
- `GetBusinessDayByDateAsync` iş gününü hâlâ `Open` döndürür,
- `GetWaiterSummariesByDateAsync` boştur,
- `GetPrintErrorSummariesByDateAsync` boştur.

## Result

Docker `alkaros-sdk10-rt8` + `alkaros-pg` üzerinde:

```bash
dotnet test tests/Modules/Reporting/V1Operations/ALKAROS.Reporting.V1Operations.Tests.csproj
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

Tam suite doğrulaması V1-GOV-047 reseal'inde çalıştırılır (11. dalga ertelenen
tam-suite doğrulaması ile birlikte).
