# V1-RMD-191 - Bill adjustments/allocations'ta eksik kullanıcı FK'ları

- Task ID: V1-RMD-191
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-189'un identity.* tarafında kapattığı boşluğun aynısını, billing
şemasında kapatır: `billing.bill_adjustments.authorized_by`/`created_by`
ve `billing.bill_allocations.created_by` — hepsi pratikte her zaman
gerçek bir `identity.users(user_id)`, ama hiçbiri bunu zorunlu kılan bir
foreign key taşımıyordu.

V1-RMD-189'un kendi "Out of scope" bölümü bunu "modüller arası FK'lar
için tutarsız bir emsal var" diyerek kapsam dışı bırakmıştı — ama bu
görevi yazarken emsali gerçekten kontrol etmek, tersini gösterdi:
`table_transfers`, `table_merges`, `table_reservations`,
`push_subscriptions`, `serving_handoff_notes`, `help_requests`,
`alerts` ve şimdi V1-RMD-189'un kendisi — hepsi zaten kendi
modüllerinden `identity.users`'a FK'lı. Modüller arası FK'lama bu
codebase'in TUTARLI normu, istisnası değil; V1-RMD-189'un varsayımı
yanlıştı, bu görev onu düzeltiyor.

Hiçbir tablo bir immutability trigger'ı taşımıyor (V1-RMD-189'un
`authorization_grants`/`behavioural_tightenings`'inin aksine), o yüzden
nullable `created_by` kolonlarında `ON DELETE SET NULL` sorunsuz.
`authorized_by` NOT NULL — bir indirim/ikram/ücreti kimin gerçekten
yetkilendirdiği, o yetkilendirenin hesabı silinse bile hayatta kalması
gereken tam da böyle bir gerçek, o yüzden düz (RESTRICT) varsayımda
kaldı.

## Owned surface

- `plan/v1/remediation/V1-RMD-191-bill-adjustments-allocations-user-fk.md` (yeni)
- `database/migrations/V1/V1-RMD-191/108-bill-adjustments-allocations-user-fk.up.sql` (yeni)
- `database/migrations/V1/V1-RMD-191/108-bill-adjustments-allocations-user-fk.down.sql` (yeni)
- Sınırlı ek:
  - database/MigrationComposition/order.json (Host Composition
    sahipliğinde) — yeni "108" pozisyonu, `phaseBRange.max` "108"e
    çekildi.
  - src/Host/Composition/Migrations/MigrationManifest.cs (Host
    Composition sahipliğinde) — `PhaseBMax` "108"e çekildi, üstteki
    yorum güncellendi.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (Host
    test sahipliğinde) — `RuntimeManifestIds`'e "108" eklendi,
    `LastEntryTables` güncellendi, toplam sayı 106'dan 107'ye çekildi.

## Out of scope

- Yok — V1-RMD-189'un kendi ertelemesinin düzeltmesi, ayrı bir kapsam
  genişletmesi değil.

## Dependencies

- V1-BIL-002
- V1-BIL-003
- V1-RMD-189

## Acceptance evidence

- Migration, `docker exec alkaros-test-pg psql` ile 001'den 108'e tüm
  sırayla taze bir scratch veritabanına uygulandı — sıfır hata, üç FK
  kısıtının hepsi `\d` çıktısında doğrulandı
  (`fk_bill_adjustments_authorized_by`, `fk_bill_adjustments_created_by`,
  `fk_bill_allocations_created_by`). `down.sql` de aynı scratch
  veritabanında hatasız çalıştı.
- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/MigrationComposition/*.csproj` → **135/135 yeşil**.
  - `tests/Modules/Billing/SplitDesign/*.csproj` → **28/28 yeşil**
    (bu proje `021-bill-adjustments.up.sql`'i hiç bağlamıyor ve
    `005-users.up.sql`'i de bağlamıyor — kendi bağladığı migration
    kümesi 108'i hiç çalıştırmıyor, dolayısıyla regresyon riski yok).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
