# V1-RMD-107 - Containerized test execution, image hygiene, and table-draft item idempotency

- Task ID: V1-RMD-107
- Status: Done
- Assignee: claude-session-011Z3dQdMVJBZEXFgDQt5i6e
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Docker zaten düzgün değil bencede doğru yapılandırılıp
ordan yürütülmeli iş. Tüm önerilerini yap.", 2026-09-06), önerilen
maddelerden ikisi bu görevde tamamlandı:

1. **Konteynerize test yürütme** — `PsqlScriptRunner` (production migration
   kodu) gerçek bir `psql` binary'sine ihtiyaç duyuyor;
   `tests/Host/MigrationComposition` bu yüzden bu makinenin bozuk Windows
   `psql.exe`'sine (eksik DLL, G2) bağımlıydı ve 40+/121 testi hep başarısız
   gösteriyordu — gerçek bir regresyon değil, ortam gürültüsü. Artık testler
   production'ın kendi Linux imajı içinde, gerçek `psql` ile, tek kullanımlık
   bir Postgres'e karşı çalışabiliyor.
2. **`V1-RMD-106`'nın kendi düzeltmesinin ortaya çıkardığı bir risk** —
   `CreateOrUpdateTableDraftAsync`'in artık kalemleri koşulsuz eklemesi,
   ağ hatası sonrası bir istemci tekrarını (WaiterPwa'nın çevrimdışı kuyruğu
   dahil) her seferinde kalemi ikiye katlardı. Kalem kimliği artık
   istemcinin zaten ürettiği kararlı sepet-satırı id'si (`crypto.randomUUID()`)
   olarak sunucuya gönderiliyor; tekrar eden bir istek mevcut satırı
   günceller, yeni bir tane eklemez.

Ayrıca Docker imaj hijyeni: geçmiş ad-hoc denetim/debug oturumlarından
kalan 11 referanssız imaj (`alkaros-rmd034-*`, `alkaros-v1-audit-*`,
`alkaros:*-audit`, `alkaros-sdk10-rt8`, `alkaros-host-build-test`, ~7GB)
silindi — hiçbir compose dosyası tarafından kullanılmıyorlardı.

## Owned surface

- `plan/v1/remediation/V1-RMD-107-containerized-test-execution-and-draft-item-idempotency.md`
- `compose.test.yaml` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - deploy/docker/Dockerfile ve compose.yaml (V1-RMD-098 sahipliğinde) —
    yeni `test` build stage'i (backend-build üzerine `postgresql-client` +
    net8.0 shared runtime katmanı) ve compose.yaml'ın üst yorum bloğuna
    `compose.test.yaml` kullanım notu eklendi; mevcut servisler
    değişmedi.
  - src/Host/Experience/Orders/OrderManagementContracts.cs ve
    OrderManagementStore.cs (V1-ORD-005 sahipliğinde, V1-RMD-106'da da
    dokunuldu) — OrderItemDraftDto artık istemcinin ürettiği `Id`'yi
    taşıyor; CreateOrUpdateTableDraftAsync onu OrderItem kimliği olarak
    kullanıyor ve birleştirme sırasında zaten kalıcı olan id'leri tekrar
    eklemekten kaçınıyor.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-CUI-004 sahipliğinde) ve
    src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-006/008
    sahipliğinde) — her sepet satırı artık zaten sahip olduğu `id`'yi
    payload'a ekliyor.
  - tests/Host/Experience/Orders/TableDraft/** (V1-RMD-106'da açılan yeni
    test yüzeyi) — retry-idempotency regresyon testi eklendi, mevcut
    testler yeni `Id` alanına güncellendi.

## In scope

1. `deploy/docker/Dockerfile`'a `backend-build` üzerine kurulu bir `test`
   stage'i: `postgresql-client` kurulu, ayrıca `dotnet test`'in net8.0 test
   host'unu başlatabilmesi için `mcr.microsoft.com/dotnet/aspnet:8.0`'dan
   kopyalanan paylaşımlı çalışma zamanı (SDK imajı yalnızca kendi 10.x
   çalışma zamanını taşıyor).
2. `compose.test.yaml`: tek kullanımlık `test-postgres` (tmpfs, kalıcı
   volume yok) + `test` servisi (`dotnet test ALKAROS.slnx -c Release`),
   `ALKAROS_TEST_PG_*` ortam değişkenleriyle — mevcut tüm test fixture'ları
   (`PgTestDatabase` ve türevleri) zaten bu değişkenleri okuyor, kod
   değişikliği gerekmedi.
3. `OrderItemDraftDto.Id` eklendi; `CreateOrUpdateTableDraftAsync` bunu
   `OrderItem.Id` olarak kullanıyor ve birleştirme kolunda zaten kalıcı
   olan id'leri `newItems`'tan filtreliyor.
4. Cashier/WaiterPwa JS'inin gönderdiği her kalem artık `id` taşıyor.
5. 11 referanssız Docker imajının silinmesi (~7GB).

## Out of scope

- Diğer önerilen maddeler (garson-masa servis ataması modeli, diğer kilit
  yollarının eşzamanlılık taraması, row_version'ın değişmeyen kalemlerde
  de artması, Cashier/WaiterPwa JS test altyapısı) — ayrı görevlere
  bırakıldı.
- `orderPayload.id` (sipariş/istek düzeyi idempotency anahtarı, hâlâ
  sunucu tarafında kullanılmıyor) — kalem düzeyi id yeterli koruma
  sağladığı için bu dalgada ele alınmadı.

## Dependencies

- V1-RMD-106

## Acceptance evidence

- `docker compose -f compose.yaml -f compose.test.yaml build test`:
  başarılı.
- `docker compose -f compose.yaml -f compose.test.yaml run --rm test`:
  **60/60 test projesi, sıfır başarısız** — `ALKAROS.Host.Tests.dll`
  (MigrationComposition) dahil **121/121** (bu makinede yerel `dotnet test`
  ile hep kısmen başarısız görünen süit; artık gürültüsüz).
- `dotnet build ALKAROS.slnx -c Debug` (yerel): 0 uyarı / 0 hata.
- `dotnet test tests/Host/Experience/Orders/TableDraft/...` (yerel):
  4/4 — yeni `RetryingAnIdenticalDraftRequestDoesNotDuplicateItems` testi
  dahil (aynı payload iki kez gönderilir, tek kalem/tek tutar kalır).
- `docker compose up --build --wait` ile ana yığın (postgres/migrate/
  provision/api/web) yeniden inşa edilip sağlıklı ayağa kalktığı
  doğrulandı; `migrate` servisi bugünkü koda karşı 0 hata ile bitti.
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.
- Semih'in elle deneyebileceği senaryo: `docker compose -f compose.yaml
  -f compose.test.yaml run --build --rm test` çalıştır — tüm test
  paketleri konteyner içinde, bu makinenin kendi `psql`/ortam
  sorunlarından bağımsız olarak geçer.

## Handoff

- V1-GOV-091
