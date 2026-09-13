# V1-IAM-028 - Mutfak ileri-geçiş izninin iptalden ayrılması ve Mutfak Personeli rolü

- Task ID: V1-IAM-028
- Status: Planned
- Assignee: Unassigned
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
- Yeni migration (`database/migrations/V1/V1-IAM-028/`, sıradaki numara
  109+, bu görevin kendi owned dosyası) — `roles` tablosuna "Mutfak
  Personeli" satırı, `role_permissions` tablosuna bu rol için yalnız
  `kitchen.advance` ataması. "Mutfak Şefi" rolünün kendisi bu görevin
  kapsamı DIŞINDA (bkz. V1-IAM-029) — bu migration yalnız Personel'i açar.
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

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Migration'ın ileri/geri (up/down) ikisi de boş veritabanında denenir.
- `dotnet test` → `tests/Host/Experience/KitchenOperations` ve
  `tests/Modules/Identity` ilgili projeleri: yeni testler dahil tümü
  yeşil, gerçek Postgres'e karşı.
- Semih'in elle deneyebileceği senaryo: "Mutfak Personeli" izniyle
  oturum açılmış bir cookie ile bir kalemi `Preparing`'e ilerlet (200),
  aynı cookie ile ticket'ı `Cancelled`'a geçirmeyi dene (403).

## Handoff

- None
