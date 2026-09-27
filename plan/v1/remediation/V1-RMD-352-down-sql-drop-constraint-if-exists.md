# V1-RMD-352 - Rollback dosyalarındaki `DROP CONSTRAINT` çağrıları artık `IF EXISTS` kullanıyor

- Task ID: V1-RMD-352
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: bazı `.down.sql` (rollback) dosyaları
`DROP CONSTRAINT`'i çıplak (`IF EXISTS` olmadan) kullanıyor. Bu depodaki 16 başka `.down.sql` dosyası zaten
`DROP CONSTRAINT IF EXISTS`'i kendi standardı olarak kullanıyor (doğrulandı: bu dosyalar bir istisna değil, bir
sapma). Çıplak form, bir rollback'in constraint'i BEKLENENDEN ÖNCE bulamadığı herhangi bir durumda (kısmen
uygulanmış bir migration, elle yapılmış önceki bir müdahale, ya da bu görevin kapsamı dışındaki bir gelecek
migration'ın aynı constraint'i erkenden kaldırması) rollback'in tamamının gereksiz yere `ERROR`'la durmasına yol
açar; `IF EXISTS`, constraint zaten mevcut olduğunda davranışı DEĞİŞTİRMEDEN (idempotent DROP), yokken sessizce
atlar.

Kod tabanının kendi `MigrationExecutor.cs`'i (`src/Host/Composition/Migrations/MigrationExecutor.cs`)
incelenerek doğrulandı: migration checksum kapısı SADECE `.up.sql` (ileri) dosyasının içeriğini
`migration_history` tablosundaki kayda karşı karşılaştırıyor — hem `apply` hem `rollback` sırasında. `.down.sql`
dosyasının içeriği HİÇBİR ZAMAN checksum'lanmıyor; her rollback'te dosya olduğu gibi yeniden okunup çalıştırılıyor.
Bu, halihazırda uygulanmış bir dağıtımın migration bütünlüğü kontrollerini bozmadan, geçmiş `.down.sql`
dosyalarının geriye dönük olarak güvenle düzenlenebileceği anlamına gelir.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V1/V1-RMD-112/074-inbox-index-and-denial-events-restrict.down.sql
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V1/V1-RMD-130/088-print-jobs-awaiting-operator-review-status.down.sql
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V1/V1-RMD-230/121-payments-cross-field-invariants.down.sql
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V1/V1-WTR-012/099-personal-comp-budget-policy-path.down.sql
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V1/V1-WTR-025/105-order-items-course.down.sql
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V12/V12-NFC-001/077-orders-nfc-source.down.sql
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V12/V12-ONL-008/154-provider-neutral-inbox-and-mapping.down.sql
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V13/V13-ALC-001/123-payment-allocations.down.sql
- `plan/v1/remediation/V1-RMD-352-down-sql-drop-constraint-if-exists.md`

## In scope

1. 8 dosyadaki toplam 13 çıplak `DROP CONSTRAINT` çağrısının tamamı `DROP CONSTRAINT IF EXISTS` olarak
   değiştirildi (`fk_denial_events_user`, `print_jobs_status_check`, `ck_payments_change_amount_reconciles`,
   `ck_payments_approved_amount_not_exceeding_tendered`, `ck_payments_status_approved_amount_pairing`,
   `ck_payments_status_tendered_amount_pairing`, `ck_authorization_grants_policy_path`,
   `order_items_kitchen_state_check`, `orders_source_check`, `uq_provider_inbox_event`, `uq_bills_id_currency`,
   `uq_payments_id_currency`, `uq_payments_id_bill`).
2. `V12-ONL-008/154-...down.sql` dosyası bir peer oturumun (farklı git worktree) sahip olduğu bir migration
   olduğundan, değişiklik SADECE bu tek `DROP CONSTRAINT` satırıyla sınırlandı; dosyanın kalanı (DO bloğu, diğer
   RENAME/CREATE mantığı) dokunulmadı.

## Out of scope

1. Bu 8 dosyanın her biri için ayrı, dosyaya özel bir "rollback sonra yeniden uygula" entegrasyon testi.
   Değişikliğin TAMAMI aynı, tek bir PostgreSQL davranışına dayanıyor (`DROP CONSTRAINT IF EXISTS`'in constraint
   yokken sessizce atlaması, `DROP CONSTRAINT`'in aynı durumda `ERROR` vermesi) — bu davranış, gerçek test
   Postgres'ine karşı doğrudan doğrulandı (aşağıdaki kanıt bölümüne bakınız). 8 dosyanın her biri için ayrı bir
   migration-zinciri kurup gerçek bir "constraint'in migration sırasının dışında eksik olduğu" senaryosu inşa
   etmek, düşük seviyeli bu bulgunun kapsamının çok ötesinde, orantısız bir efor olurdu; düzeltmenin kendisi
   satır satır mekanik ve tek bir doğrulanmış SQL deseninin (bu depoda zaten 16 dosyada kurulu, kanıtlanmış norm)
   tekrarı.

## Dependencies

- None

## Acceptance evidence

- `find database/migrations -iname "*.down.sql" | xargs grep -Hn "DROP CONSTRAINT" | grep -v "IF EXISTS"`:
  değişiklik öncesi 13 çıplak eşleşme, değişiklik sonrası 0 eşleşme.
- `find database/migrations -iname "*.down.sql" | xargs grep -l "DROP CONSTRAINT IF EXISTS" | wc -l`:
  değişiklik öncesi 16, değişiklik sonrası 24 (16 + bu görevin dokunduğu 8 dosya).
- Gerçek test Postgres'ine (`alkaros-test-pg`, port 55432) karşı canlı bir doğrulama: geçici bir şema/tablo/
  constraint oluşturulup ilk `DROP CONSTRAINT` (IF EXISTS olmadan) başarıyla çalıştırıldı, AYNI constraint'in
  ikinci kez çıplak `DROP CONSTRAINT` denemesi gerçek `ERROR: constraint "..." of relation "t" does not exist`
  ile başarısız oldu (eski formun tam olarak önlemeye çalıştığımız kırılganlığı), aynı ikinci deneme
  `DROP CONSTRAINT IF EXISTS` ile sessizce (NOTICE + no-op) başarılı oldu. Geçici şema temizlendi
  (`DROP SCHEMA ... CASCADE`).
- `src/Host/Composition/Migrations/MigrationExecutor.cs` kod incelemesi: rollback checksum kontrolü SADECE
  `.up.sql` dosyasını checksum'lıyor, `.down.sql` içeriğini asla checksum'lamıyor — bu 8 geçmiş dosyanın
  düzenlenmesinin mevcut `migration_history` bütünlük kontrollerini bozmayacağını doğrular.

## Handoff

- None
