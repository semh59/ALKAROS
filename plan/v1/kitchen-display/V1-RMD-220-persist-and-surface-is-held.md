# V1-RMD-220 - `IsHeld` artık kalıcı ve KDS ekranında görünür

- Task ID: V1-RMD-220
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **HIGH** bulgu:
`KitchenTicketItem.IsHeld` (V1-WTR-025'in kurs modeli, "bu kalem bir
sonraki kurs için bekletiliyor") API sözleşmesine (`KitchenTicketItemV1`)
hiç taşınmamıştı. Kod incelemesi sırasında bulgunun ima ettiğinden daha
derin bir kök neden ortaya çıktı: `IsHeld` veritabanına **hiç
yazılmıyordu** — yalnızca `KitchenTicket.CreateFromOrder`'ın kurduğu
bellek-içi nesnede vardı; ticket kaydedilip yeniden okunduğu an
(gerçek KDS ekranının HER okuması) her zaman `false` dönüyordu. DTO'ya
alan eklemek tek başına hiçbir şey değiştirmeyecekti — kalıcılık
olmadan sözleşme alanı kozmetik kalırdı.

## Owned surface

- database/migrations/V1/V1-RMD-220/** (yeni)
- src/Modules/Kitchen/TicketLifecycle/PostgresKitchenTicketRepository.cs
  (ilgili modülün sahipliğinde) — is_held artık okunuyor/yazılıyor.
- src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs,
  KitchenOperationsStore.cs (ilgili modül) — yeni alan DTO'ya taşındı.
- src/Clients/PosTerminal/src/features/kitchen-operations/models.ts,
  KitchenOperationsWorkspace.tsx, kitchen-operations.css (ilgili modül)
  — yeni "⏸ Kurs bekliyor" rozeti.
- tests/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs,
  src/Clients/PosTerminal/.../KitchenOperationsWorkspace.test.tsx
  (ilgili modüller)

Sınırlı ek (yollar geri-tik olmadan):

- database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs (paylaşılan) —
  yeni migration pozisyonu 113 kaydedildi, `PhaseBMax` güncellendi
  (V1-RMD-159/V1-IAM-030 emsali).
- tests/Host/MigrationComposition/Manifest/ManifestTests.cs (ilgili
  test projesi) — sabit sayı/id listesi 113'ü yansıtacak şekilde
  güncellendi.
- tests/Host/Experience/{NfcOrdering,Orders/Confirmation,Orders/TableDraft,
  Orders/VoidSent}/**, tests/Modules/Kitchen/{PhysicalPrintRecovery,
  PrintQueue}/** (ilgili test projeleri) — `kitchen_ticket_items`
  tablosunu kullanan her fixture listesine yeni migration eklendi
  (aynı tablonun kardeş migration'ı 092 zaten oradaydı).
- database/migrations/V1/V1-KIT-001/014-kitchen-tickets.{up,down}.sql,
  V1-OPS-001/015-audit-log.{up,down}.sql (paylaşılan, kök neden
  düzeltmesi) — bu 4 dosyada bu görevden ÖNCEYE ait bir UTF-8 BOM
  vardı; Windows'ta yazılmış olmalı, `psql` 18.6 bunu "syntax error"
  olarak reddediyordu. `tests/Host/MigrationComposition`'ın hiç
  çalıştırılmamış olması yüzünden bu oturuma kadar hiç fark edilmemiş
  bir ortam sorunuydu (fixture-tabanlı testler ham SQL metnini Npgsql
  üzerinden çalıştırdığı için BOM'dan etkilenmiyorlardı). Yalnızca
  BOM baytları kaldırıldı, SQL içeriği değişmedi.

## In scope

1. Yeni migration 113: `kitchen.kitchen_ticket_items`'a `is_held
   BOOLEAN NOT NULL DEFAULT FALSE` — `is_age_restricted`'ın (V1-RMD-137)
   aynı "baskı-anı anlık görüntüsü, hiç güncellenmiyor" ilkesiyle.
2. `PostgresKitchenTicketRepository`: hem `AddAsync` hem `SaveAsync`'in
   INSERT/UPSERT'i artık `is_held`'i yazıyor; her iki SELECT/hydrate
   yolu da (`LoadItemsAsync`, `LoadTicketGraphAsync`) okuyor.
3. `KitchenTicketItemV1.IsHeld`, `ToDto`'da dolduruluyor.
4. Frontend: `KitchenTicketItem.isHeld`, ürün adının yanında turuncu
   bir "⏸ Kurs bekliyor" rozeti.
5. Yeni testler: gerçek Postgres'e karşı kaydet+yeniden-yükle testi
   (`IsHeldSurvivesASaveAndReloadThroughPostgres`); frontend'de rozet
   testi.

## Out of scope

- `KitchenTicketItem.CourseNumber` — aynı kalıcılık boşluğu bu alanda
  da var (repository hiç yazmıyor/okumuyor), ama bu bağımsız denetimin
  bulgusu değildi; ayrı bir görev gerektirir.
- `tests/Host/MigrationComposition`'ın kendi 4 kalan başarısızlığı
  (`HousekeepingTests`, `KvkkRetentionTests`, `MigrationExecutionTests.
  ProgramMainProducesTheDocumentedExitCodes`) — bunlar `ALKAROS.Host.
  Program.Main`'in CLI alt komutlarını (housekeeping, kvkk-retention)
  sentetik bir geçici migration setiyle çağırıyor, gerçek migration
  içeriğiyle hiç ilgisi yok; bu oturumdan önce de var olan, ortama
  özgü ayrı bir sorun (muhtemelen `--db-url` ayrıştırma/parola kimlik
  doğrulaması). Bu görevin kapsamı dışında.

## Dependencies

- V1-WTR-025
- V1-RMD-137

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Modules/Kitchen/TicketLifecycle` →
  29/29 yeşil (yeni kalıcılık testi dahil); revert-and-confirm ile
  gerçekten kırılıp doğrulandı (yük: is_held=false yazıldığında
  reload sonrası False dönüyor).
- Regresyon taraması, hepsi yeşil: `tests/Host/Experience/{KitchenOperations,
  Orders/TableDraft,Orders/Confirmation,Orders/VoidSent,NfcOrdering}`,
  `tests/Modules/Kitchen/{PhysicalPrintRecovery,PrintQueue}`.
- `tests/Host/MigrationComposition` → 131/135 yeşil (kalan 4, kapsam
  dışı bırakılan pre-existing CLI sorunu — yukarıya bakın); gerçek
  migration setinin uçtan uca uygulanmasını doğrulayan testler
  (`DualScreenStoreTests`, `ManifestTests`, `KvkkRetentionTests`'in
  kendi `InitializeAsync`'i) hepsi yeşil.
- `npx tsc --noEmit` (PosTerminal) → 0 hata.
- `npx vitest run` (PosTerminal, tüm proje) → 173/173 yeşil, yeni rozet
  testi dahil; revert-and-confirm ile doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
