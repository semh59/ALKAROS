# V1-RMD-026 doğrulama kanıtı

## Ortam ve sınır

- Repository root: `D:\PROJECT\ALKAROS`.
- Active task: `V1-RMD-026`.
- .NET SDK: `10.0.302`.
- PostgreSQL: `postgres@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2`.
- Disposable test container: `alkaros-rmd026-pg-20260828`, host port `55436`.
- Başlangıç `git status --short` ve `git diff --name-only` görüntüsü alındı. Mevcut kullanıcı ve önceki görev
  değişiklikleri korunarak yalnız task owned surface değiştirildi.

## Teslim edilen davranış

- Migration `039`; `zone_floor_plans`, `table_layouts` ve kalıcı kimlikli `table_seats` tablolarını ekler.
- V1 şekilleri `Rectangle`, `Round` ve `Square`; rotation değerleri `0/90/180/270`; canvas, masa ve sandalye
  koordinatları bounded doğrulanır.
- Floor save; zone, floor-plan, table lifecycle, layout ve seat version'larını tek serializable transaction içinde
  doğrular. Stale version `409` üretir ve kısmi yazım bırakmaz.
- Merge dışı masa overlap'i reddedilir; aynı active merge grubundaki masalara overlap izni verilir.
- Kapasite ile aktif sandalye sayısı farkı veri uydurularak düzeltilmez; `CAPACITY_SEAT_MISMATCH` uyarısı döner.
- GET snapshot'ı `RepeatableRead` kullanır; table order/bill pointer, active reservation, active merge ve allowed-command
  bağlamını personel, token, secret veya müşteri verisi eklemeden döndürür.
- Floor GET/PUT endpoint'leri mevcut terminal-bound cashier session ve `pos.cashier.mutate` permission sınırını kullanır;
  eksik session `401`, eksik permission `403` olur.

## Kod inceleme sonucu

`code-reviewer` güvenlik, concurrency, performans ve bakım turu şu düzeltmeleri kapanış öncesinde üretti:

- Çok sorgulu floor read'in karışık revision döndürmesini önlemek için `RepeatableRead` snapshot eklendi.
- Geometri kaydının stale table capacity/state üzerinde ilerlememesi için `ExpectedTableRowVersion` zorunlu yapıldı.
- İki kalıcı sandalye numarasının aynı transaction'da yer değiştirmesi için unique constraint
  `DEFERRABLE INITIALLY DEFERRED` yapıldı.
- Eski zone'dan taşınmış bir masanın layout version'ı table ID üzerinden kilitlenerek yeni zone kaydında PK conflict
  yerine versioned update uygulanması sağlandı.
- SQL sorgularında kullanıcı girdisi interpolation ile kullanılmıyor; bütün değerler Npgsql parameter'ıdır.

## Build ve test

- `dotnet build ALKAROS.slnx --configuration Release --no-restore`: exit code `0`, 0 warning, 0 error.
- `dotnet test tests/Modules/Tables/TableLifecycle/ALKAROS.Tables.TableLifecycle.Tests.csproj --configuration Release
  --no-build`: exit code `0`, 55/55 test geçti.
- `dotnet test tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj --configuration Release
  --no-build`: exit code `0`, 8/8 test geçti.
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --configuration Release --no-build --filter
  FullyQualifiedName~ManifestTests`: exit code `0`, 16/16 test geçti.
- İlk full-solution test denemesinde Windows PATH üzerinde `psql` bulunmadığı için 29 Host testi başlamadı. Sistem
  kurulumu yapılmadı; `evidence/V1-RMD-026/psql-proxy/**` test-only proxy'si stdin/stdout ve `--file` davranışını aynı
  PostgreSQL 18 container'ındaki gerçek `psql` sürecine iletti.
- Proxy ile `dotnet test ALKAROS.slnx --configuration Release --no-build --no-restore`: exit code `0`; solution'daki
  bütün test projeleri 0 failure ile geçti. Host MigrationComposition paketi 93/93 geçti.
- `dotnet format ALKAROS.slnx --verify-no-changes --no-restore --include <V1-RMD-026 code/test paths>`: exit code `0`.

## Migration ve performans

- Boş PostgreSQL 18 veritabanına manifest sırasıyla `001..039` uygulandı: exit code `0`.
- `039-floor-plan.down.sql`: exit code `0`; üç tablonun `to_regclass` sonucu null.
- `039-floor-plan.up.sql` yeniden uygulandı: exit code `0`; üç tablo geri geldi ve seat unique constraint için
  `condeferrable AND condeferred = true` doğrulandı.
- Gerçek HTTP senaryosu zone ve masaları oluşturdu; sandalyeleri konumlandırdı; overlap'i reddetti; merge overlap'ini
  kabul etti; stale floor/table version'larında `409` verdi; seat number swap'ini kaydetti ve Host restart sonrası
  aynı geometry/seat kimliklerini yeniden okudu.
- Temsilî 100 masa ve 400 sandalye dataset'inde read join'i `EXPLAIN (ANALYZE, BUFFERS)` ile ölçüldü: 400 row,
  planning `3.643 ms`, execution `1.525 ms`, shared hit `18`. Onaylı SLO bulunmadığından bu ölçüm production performans
  iddiası veya eşik kabulü değildir.

## Governance

- `python tools/plan-audit/plan_audit_tool.py validate`: exit code `0`, 0 hata ve 0 uyarı.
- Owned surface `git diff --check`: exit code `0`.
- Ürün henüz production-ready değildir. Sıradaki dependency görevi `V1-RMD-027` operasyonel hesap bölme API'sidir.
