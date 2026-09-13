# V1-IAM-028 - Mutfak ileri-geçiş izninin iptalden ayrılması ve Mutfak Personeli rolü

- Task ID: V1-IAM-028
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in kararı (2026-09-13, Mutfak ekranı yeniden tasarımı gereksinim
toplama oturumu): düz mutfak çalışanı ("Mutfak Personeli") bir mutfak
biletinde/kaleminde YALNIZ ileri geçiş yapabilsin (Queued→Preparing→
Ready→Served); iptal, sorun bildirme, ürün tükendi bildirimi, yazıcı/rota
yönetimi gibi diğer her şey "Mutfak Şefi" rolünde kalsın. Bugün
`KitchenOperationsEndpoints.cs:29`'daki tek `TicketMutationPermission`
(`ApplicationPermissions.OrdersSend`) hem ileri geçişi hem iptali aynı
izinle koruyor — bu ayrım olmadan Personel'e "yalnız ileri" bir yetki
verilemez. Yeni bir dar kapsamlı `kitchen.advance` izni eklenir; hedef
durum `Cancelled` olduğunda mevcut `orders.send` istenmeye devam eder.
Mevcut dört rol (garson/kasiyer/şef garson/yönetici) her ikisini de
(orders.send zaten var, kitchen.advance yeni eklenir) taşır — geriye dönük
davranış değişmez.

## Owned surface

- src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  (Sınırlı ek — V1-IAM-024 sahipliğinde kalan dosya) — yeni
  `KitchenAdvance = "kitchen.advance"` sabiti, `RoleGrants` sözlüğünün
  dört mevcut role bu izni de eklemesi. `kitchen.reprint`/
  `kitchen.routing.manage` emsaline uyarak `kitchen.advance`'in kendisi
  bu dosyada TANIMLANIR ama Kitchen'a özgü diğer izinler (bkz.
  `V1-KIT-008`) bu dosyaya eklenmez — Kitchen modülü kendi ek izinlerini
  zaten `KitchenOperationsEndpoints.cs` içinde yerel sabit olarak tanımlıyor
  (bağımsız denetim Y2'de doğrulandı), bu görev o emsali bozmaz.
- src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs
  (Sınırlı ek — V1-RMD-082 sahipliğinde kalan dosya) —
  `/tickets/{id}/transition` ve `/tickets/{id}/items/{id}/transition`
  uçlarının, `TargetState == "Cancelled"` ise `orders.send`, aksi halde
  `kitchen.advance` istemesi.
- Yeni migration (`database/migrations/V1/V1-IAM-028/**`, numara 109, bu
  görevin kendi owned dosyası) — `roles` tablosuna "Mutfak Personeli"
  (`kitchen-staff`) satırı, `role_permissions` tablosuna bu rol için
  yalnız `kitchen.advance` ataması. "Mutfak Şefi" rolünün kendisi bu
  görevin kapsamı DIŞINDA (bkz. V1-IAM-029) — bu migration yalnız
  Personel'i açar.
- database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs
  (mekanik zorunlu eş-güncelleme — yeni bir migration pozisyonu eklemenin
  ayrılmaz parçası, hafıza notundaki bilinen tuzak: ikisi de "108" →
  "109" olarak güncellenmezse `MigrationComposition` test paketi kapalı
  kalır) — bu görevin başında Owned surface'a dahil edilmemişti,
  yürütme sırasında eklendi.
- tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs,
  PermissionSplitDatabase.cs, PermissionSplitDatabaseTests.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (mekanik
  zorunlu eş-güncelleme — `ApplicationPermissions.Codes`/`RoleGrants`
  ve migration manifest'i değişince bu dosyaların sabit sayı/küme/id-listesi
  beklentileri güncellenmez ise test paketi kırılır) — bu görevin başında
  Owned surface'a dahil edilmemişti, yürütme sırasında eklendi.
- docs/domain/authorization-model.md (Sınırlı ek, paylaşılan) — izin
  tablosuna `kitchen.advance` satırı, `orders.send`'in mutfak
  bağlamındaki kullanımının da (bugün zaten var, dokümante edilmemiş)
  not düşülmesi.
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (Sınırlı ek — V1-RMD-082 sahipliğinde kalan dosya) —
  `SeedSessionAsync` çağrılarının hangi izinle hangi senaryoyu kapsadığının
  gözden geçirilmesi (bkz. bağımsız denetim Y3: bugün tek izinle seed
  ediliyor); en az bir test `kitchen.advance`-yalnız bir oturumun ileri
  geçişi yapıp iptali reddedildiğini kanıtlamalı.

## Out of scope

- "Mutfak Şefi" rolünün kendisi ve onun ek izinleri (`V1-IAM-029`).
- Frontend'in bu yeni izne göre buton gizleme/kilitleme davranışı
  (`V1-KDS-001`).
- İstasyon bazlı yetki kısıtlaması ("Personel yalnız İzgara'da çalışsın") —
  bağımsız denetimde (O3) bulunan ayrı bir açık soru, bu görevin kapsamı
  dışında.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → **Oluşturma başarılı oldu. 0
  Uyarı, 0 Hata.**
- Migration 109 gerçek `ALKAROS.Host.dll` migration runner'ıyla (psql
  18.6) boş bir scratch veritabanında (`alkaros_iam028_smoke`) uçtan uca
  denendi: **ileri** — 001'den 109'a kadar tüm zincir (`--order-manifest
  database/MigrationComposition/order.json --migrations-dir
  database/migrations`) "All 108 migration(s) verified; 108 applied.
  exit: 0"; ardından `identity.permissions`/`identity.roles`/
  `identity.role_permissions` sorgulanıp `kitchen.advance` ve
  `kitchen-staff`'ın 5 role (waiter/cashier/supervisor/manager/
  kitchen-staff) doğru atandığı doğrulandı. **Geri** — `--rollback 109`
  "[109] rolled back ... exit: 0"; ardından her iki satırın da 0'a
  döndüğü (cascade ile `role_permissions` de temizlendi) doğrulandı.
  Scratch veritabanı sonra silindi.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test`:
  - `tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj`
    → **Başarılı! Başarısız: 0, Başarılı: 9, Atlanan: 0, Toplam: 9**
    (izin ayrımını doğrulayan `KitchenAdvanceOnlySessionCanAdvanceButNotCancel`
    dahil, gerçek Postgres'e karşı).
  - `tests/Modules/Identity/Authorization/ALKAROS.Identity.Authorization.Tests.csproj`
    → **Başarılı! Başarısız: 0, Başarılı: 199, Atlanan: 0, Toplam: 199**
    (17 kod/`kitchen-staff` rolü dahil güncellenen `ApplicationPermissionsTests`/
    `PermissionSplitDatabaseTests`, gerçek Postgres'e karşı).
  - `tests/Host/Experience/Authorization/ALKAROS.Host.Experience.Authorization.Tests.csproj`
    → **Başarılı! 5/5.**
  - `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` →
    **Başarılı! Başarısız: 0, Başarılı: 135, Atlanan: 0, Toplam: 135**
    (manifest 108→109 pozisyon/tablo güncellemesi dahil).

**Düzeltmeler (2026-09-13, bağımsız kod denetimi sonrası):**

1. **plan-audit gerçekte hata veriyordu**: Owned surface'taki migration
   yolu (`database/migrations/V1/V1-IAM-028/`) sonunda `/**` eksikti,
   audit tool'u bunu dosyalarla eşleşmeyen bir desen olarak okuyup
   `UNOWNED_PRODUCTION_FILE` hatası veriyordu (migration commit'ten önce
   audit çalıştırılmış, sonra tekrar çalıştırılmamış). `/**` eklendi,
   `plan_audit_tool.py validate` yeniden çalıştırılıp gerçekten **0
   hata, 0 uyarı** doğrulandı.
2. **Gerçek yetki atlatma açığı bulundu ve kapatıldı**: `TicketTransitionPermission`
   `string.Equals` ile boşluk kırpmadan karşılaştırıyordu, ama domain'in
   `Enum.TryParse` ayrıştırması (.NET'in dokümante davranışı) boşluk
   kırpıyor — `TargetState: "Cancelled "` gibi bir istek yetki kapısında
   "Cancelled değil" sanılıp yalnız `kitchen.advance` isteniyordu, ama
   domain yine de gerçekten iptal ediyordu. **Önce açığı boşlukla kanıtladım**
   (düzeltmeyi `git stash` ile geçici kaldırıp `KitchenAdvanceOnlySessionCannotCancelViaWhitespaceOrCaseVariants`
   testini çalıştırdım — `"Cancelled "` ve `" Cancelled"` varyantları
   gerçekten 200 OK döndü, açık doğrulandı), sonra `.Trim()` eklenip
   düzeltme geri getirildi, aynı test 3/3 yeşil oldu
   (`ALKAROS.Host.Experience.KitchenOperations.Tests`: 12/12 toplam).
3. Bir kod yorumunda yanlışlıkla Türkçe karakter bulundu, İngilizceye
   çevrildi (`consistency_audit.py` yeniden `clean` doğrulandı).
4. `docs/domain/lifecycle-transition-contracts.md` senkronizasyonu için
   bkz. `V1-KIT-007`'nin Owned surface'ı (bu görevin değil, o görevin
   davranışının dokümanı).
- `python tools/consistency-audit/consistency_audit.py` → `clean` (bir
  kod yorumundaki yanlışlıkla yazılmış Türkçe karakter bulundu ve
  düzeltildi).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- Semih'in elle deneyebileceği senaryo: "kitchen-staff" (Mutfak
  Personeli) izniyle oturum açılmış bir cookie ile bir kalemi
  `Preparing`'e ilerlet (200), aynı cookie ile ticket'ı `Cancelled`'a
  geçirmeyi dene (403) — bu tam olarak `KitchenAdvanceOnlySessionCanAdvanceButNotCancel`
  testinin gerçek HTTP çağrısıyla kanıtladığı senaryo.

## Handoff

- None
