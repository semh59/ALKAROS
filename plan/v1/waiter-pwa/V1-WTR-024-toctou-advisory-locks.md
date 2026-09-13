# V1-WTR-024 - TOCTOU yarışlarına advisory lock düzeltmesi

- Task ID: V1-WTR-024
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatıyla ("Düzelt mimari zayıflığı", 2026-09-11): V1-WTR-023'ün
bağımsız incelemesinde bulunup bilinçli olarak ertelenen TOCTOU
(check-then-insert, kilitleme yok) yarış koşulu — kişisel ikram bütçesi
günlük tavanı (`AuthorizationGrantService`/
`PersonalCompBudgetEscalationResolver`, `auto_within` politika sayacı da
aynı riski taşıyor) ve yardım-çağır 2 dakikalık masa bekleme süresi
(`HelpRequestStore`) — artık gerçek bir kilitleme mekanizmasıyla
kapatıldı.

**Çözüm: PostgreSQL advisory lock, ilgili anahtarla sınırlı.** Bir
transaction/session boyunca sürüyor, yalnızca AYNI anahtarı (istek sahibi

- izin kodu; masa id'si) paylaşan eşzamanlı istekleri seri hale getiriyor
— farklı kullanıcılar/masalar arasında hiçbir gecikme yok. Anahtar
sunucu tarafında `hashtext()` ile hesaplanıyor (istemci tarafı .NET
hash'i process'e göre rastgele olduğu için birden fazla Host örneği
arasında güvenilir olmazdı).

- `IAuthorizationGrantRepository.AcquireRequesterLockAsync` — özel bir
  bağlantıda session-level `pg_advisory_lock`, `IAsyncDisposable` ile
  serbest bırakılıyor (`pg_advisory_unlock_all()`). `AuthorizationGrantService
  .RequestAsync`, kendi-check guard'ından sonra, tüm politika/eskalasyon
  akışını ve `StoreAsync`'i saran bir `await using` ile alıyor.
- `HelpRequestStore.RaiseAsync` zaten tek bir bağlantı kullanıyordu — artık
  açık bir transaction'a alındı, ilk iş olarak masa id'sine göre
  transaction-scoped `pg_advisory_xact_lock` alıyor (commit/rollback'te
  otomatik serbest kalıyor).

**Her ikisi de gerçekten kanıtlandı, körü körüne "eklendi" denmedi:**
kilidi geçici olarak no-op'a çevirip testleri tekrar çalıştırdım.
`HelpRequestStore`'un HTTP seviyesindeki eşzamanlılık testi kilitsiz 5/5
başarısız oldu — gerçekten kanıtlıyor. `AuthorizationGrantService`'in
HTTP seviyesindeki testi ise kilitsiz bile 5/5 GEÇTİ — iki bağımsız HTTP
gidiş-dönüşü arasındaki yarış penceresi, şans eseri yakalanamayacak
kadar dar olduğu için. Bu yüzden repository seviyesinde, yapay bir
gecikmeyle etkileşimi zorlayan deterministik bir test eklendi
(`AcquireRequesterLockSerializesTheSameRequesterAndPermission`) —
bu da kilitsizken doğrulanmış şekilde başarısız oluyor, kilitliyken
geçiyor. HTTP seviyesindeki test, gerçek kanıt olarak değil, meşru bir
uçtan uca iş-kuralı testi olarak koruma altına alındı; kendi yorum
satırı artık bunu açıkça söylüyor.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-024-toctou-advisory-locks.md` (yeni)
- Sınırlı ek:
  - src/Modules/Identity/Authorization/Grants/IAuthorizationGrantRepository.cs,
    PostgresAuthorizationGrantRepository.cs, AuthorizationGrantService.cs
    (Identity sahipliğinde) — `AcquireRequesterLockAsync`, çağrı sırası.
  - src/Host/Experience/HelpRequests/HelpRequestStore.cs (Host
    sahipliğinde) — açık transaction + `pg_advisory_xact_lock`.
  - tests/Modules/Identity/Authorization/Grants/PostgresAuthorizationGrantRepositoryTests.cs
    (Identity test sahipliğinde) — 2 yeni deterministik test.
  - tests/Host/Experience/Orders/Comp/OrderManagementCompHttpTests.cs
    (Host test sahipliğinde) — 1 yeni test + dürüst yorum düzeltmesi.
  - tests/Host/Experience/HelpRequests/HelpRequestHttpTests.cs (Host test
    sahipliğinde) — 1 yeni test.

## Out of scope

- `IPrePolicyGate` (davranışsal sıkılaştırma) kendi okuma yollarının
  aynı sınıf bir yarışı taşıyıp taşımadığı incelenmedi — bu görevin
  kilidi zaten onu da sarıyor (aynı `await using` bloğunun içinde), ama
  ayrı bir doğrulama yapılmadı.
- Bağlantı havuzu etkisi: her `AcquireRequesterLockAsync` çağrısı, kilit
  süresince ayrı, özel bir bağlantı açık tutuyor. Yoğun eşzamanlı grant
  trafiği altında havuz baskısını artırabilir — mevcut havuz
  boyutlandırmasının yeterli olduğu varsayıldı, ölçülmedi.

## Dependencies

- V1-WTR-012
- V1-WTR-014
- V1-WTR-023

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Modules/Identity/Authorization` → 198/198 (2 yeni test:
    aynı istek sahibi/izin için ikinci bir kilit alma girişimi ilkinin
    serbest bırakılmasına kadar gerçekten bloke olur — yapay 300ms
    gecikmeyle kanıtlandı; farklı istek sahipleri/izinler asla
    birbirini bloke etmez).
  - `tests/Host/Experience/HelpRequests` → 7/7 (1 yeni test: aynı masa
    için gerçekten eşzamanlı iki istek tam olarak bir kazanan üretir —
    kilit no-op'a çevrilip 5/5 başarısız olduğu doğrulandıktan sonra
    gerçek kilitle geçtiği onaylandı).
  - `tests/Host/Experience/Orders/Comp` → 14/14 (1 yeni test, dürüst
    kapsam notuyla — gerçek kanıt repository seviyesindeki testte).
  - `tests/Host/Experience/Orders/VoidSent` → 14/14, `tests/Host/
    Experience/Billing` → 18/18 (regresyon kontrolü, AuthorizationGrantService
    bu iki projede de yoğun kullanılıyor).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
