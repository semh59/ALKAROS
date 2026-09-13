# V1-RMD-182 - `FireCourse`'da eksik `IsOpenCheck` koruması

- Task ID: V1-RMD-182
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin backend
boyutundaki bulgusunu kapatır: `Order.FireCourse` (V1-WTR-025, bir
Held kursu çağırma), aynı aggregate'teki `FireRound`/`Submit`/her
durum-değiştiren metodun aksine hiçbir `IsOpenCheck` koruması
taşımıyordu.

Bir sipariş `Cancelled`'a geçtiğinde, daha önce ateşlenmiş bir turun
sonraki kursu (`Held` durumunda) hâlâ üzerinde kalabilir — check iptal
edilmeden önce hiç çağrılmamış bir kurstur. Koruma olmadan
`FireCourse`, iptal edilmiş bir sipariş üzerinde bile bu kalemleri
`Sent`'e yükseltip "ateşlenmiş kalemler" döndürüyordu; çağıran taraf
(`OrderSubmissionCoordinator.FireCourseAsync`) bunun için taze bir
mutfak fişi bastırırdı — artık kimsenin ödemediği bir siparişin bir
kursunu mutfağa pişirtmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-182-firecourse-open-check-guard.md` (yeni)
- Sınırlı ek:
  - src/Modules/Orders/OrderAggregate/Order.cs (Orders modülü
    sahipliğinde) — `FireCourse`'un en başına `FireRound`'daki
    `IsOpenCheck` kontrolünün aynısı eklendi.
  - tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs (Orders
    modülü test sahipliğinde) — yeni
    `FiringACourseOnACancelledOrderThrows` testi.

## Out of scope

- Yok — tek metotluk, tek koruma eksikliği.

## Dependencies

- V1-WTR-025

## Acceptance evidence

- Yeni domain testi (`FiringACourseOnACancelledOrderThrows`):
  - Düzeltme YOKKEN (`git stash` ile geçici kaldırılarak): FAILED, gerçek
    kanıtla ("Expected a System.InvalidOperationException to be thrown,
    but no exception was thrown").
  - Düzeltme VARKEN: passed.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Modules/Orders/OrderAggregate/*.csproj` → **126/126 yeşil**.
  - `tests/Host/Experience/Orders/TableDraft/*.csproj` → **67/67 yeşil**
    (regresyon yok — `FireCourse`'un HTTP yolu, `IsOpenCheck` her zaman
    doğru olan açık siparişler üzerinde test ediliyor, bu değişiklikten
    etkilenmedi).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
