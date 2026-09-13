# V1-RMD-189 - Kimlik yetkilendirme tablolarında eksik kullanıcı FK'ları

- Task ID: V1-RMD-189
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin database schema
boyutundaki bulgusunu kapatır: `identity.authorization_grants`,
`identity.authorization_delegations`, `identity.behavioural_tightenings`,
`identity.authorization_policies` — dördü de bir veya daha fazla UUID
kolonu taşıyordu (kim istedi, kim onayladı, kim devretti, kim
düzeltti...) ki bu kolon pratikte her zaman gerçek bir
`identity.users(user_id)`, ama hiçbiri bunu zorunlu kılan bir foreign
key taşımıyordu. Bu codebase'deki HER BAŞKA modül kendi "kim yaptı"
kolonunu zaten `identity.users`'a FK'lıyor (`table_transfers`,
`table_merges`, `table_reservations`, `push_subscriptions`,
`serving_handoff_notes`, `help_requests`, `alerts`,
`026-typed-settings`'in `changed_by`'ı...) — bu dört tablo bilinçli bir
istisna değildi, gözden kaçmış bir boşluktu.

`authorization_grants` ve `behavioural_tightenings`'in ikisi de kendi
append-once/append-once-clear trigger'ını taşıyor — zaten çözülmüş bir
satıra HERHANGİ bir UPDATE'i koşulsuz reddediyor, `ON DELETE SET NULL`
cascade'inin kendisinin göndereceği bir UPDATE dahil. `ON DELETE`
maddesi olmayan varsayılan RESTRICT, bu UPDATE'i hiç tetiklemeden
kaçınıyor: grant/tightening geçmişi olan bir kullanıcıyı silmek
doğrudan reddediliyor — kalıcı denetim verisi için doğru davranış
zaten bu. `authorization_policies` böyle bir trigger taşımıyor
(bir policy satırı normalde düzenlenir) ve `updated_by`'ı
`026-typed-settings.up.sql`'in kendi `changed_by` kolonuyla birebir
aynı şekle sahip, o yüzden aynı `ON DELETE SET NULL`'u aldı.

## Owned surface

- `plan/v1/remediation/V1-RMD-189-authorization-tables-user-fk.md` (yeni)
- `database/migrations/V1/V1-RMD-189/107-authorization-tables-user-fk.up.sql` (yeni)
- `database/migrations/V1/V1-RMD-189/107-authorization-tables-user-fk.down.sql` (yeni)
- Sınırlı ek:
  - database/MigrationComposition/order.json (Host Composition
    sahipliğinde) — yeni "107" pozisyonu, `phaseBRange.max` "107"ye
    çekildi.
  - src/Host/Composition/Migrations/MigrationManifest.cs (Host
    Composition sahipliğinde) — `PhaseBMax` "107"ye çekildi, üstteki
    yorum güncellendi.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (Host
    test sahipliğinde) — `RuntimeManifestIds`'e "107" eklendi,
    `LastEntryTables` yeni son pozisyonun tablolarına güncellendi,
    `RuntimeManifestContainsOnlyImplementedMigrationPairs`'ın beklediği
    toplam sayı 105'ten 106'ya çekildi.

## Out of scope

- `permission_code` kolonlarının `identity.permissions(code)`'a FK'lanması:
  bu audit bulgusunun kapsamı yalnızca kullanıcı referansı boşluklarıydı;
  ayrı, daha geniş bir iyileştirme olabilir.
- `billing.bill_adjustments`/`bill_allocations`'daki benzer boşluklar:
  cross-module FK'lar için bu codebase'de tutarsız bir önceki emsal var
  (bazı modüller arası referanslar bilinçli olarak DB seviyesinde
  zorlanmıyor, uygulama seviyesinde tutuluyor) — ayrı bir karar gerektirir,
  bu görevin kapsamı yalnızca in-module (identity şeması içi) boşluklar.

## Dependencies

- V1-IAM-001
- V1-IAM-018
- V1-IAM-019
- V1-IAM-021
- V1-IAM-023

## Acceptance evidence

- Migration, `docker exec alkaros-test-pg psql` ile 001'den 107'ye tüm
  sırayla taze bir scratch veritabanına uygulandı — sıfır hata, dört FK
  kısıtının hepsi `\d` çıktısında doğrulandı
  (`fk_authorization_grants_requester/approver/subject_serving_user`,
  `fk_authorization_delegations_grantee/delegator`,
  `fk_behavioural_tightenings_user/cleared_by`,
  `fk_authorization_policies_updated_by`). `down.sql` de aynı scratch
  veritabanında hatasız çalıştı (4 `ALTER TABLE`, hepsi başarılı).
- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/MigrationComposition/*.csproj` → **135/135 yeşil**
    (manifest sırası, faz aralıkları, tekil pozisyon doğrulaması dahil).
  - `tests/Modules/Identity/Authorization/*.csproj` → ayrıca çalıştırıldı,
    bkz. commit mesajı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
