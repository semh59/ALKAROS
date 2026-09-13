# V1-IAM-029 - Mutfak Şefi rolü

- Task ID: V1-IAM-029
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Goal

Semih'in kararı (2026-09-13): mevcut "şef garson" (`supervisor`, önyüz kat
sorumlusu) ile karıştırılmayacak, bağımsız bir "Mutfak Şefi" (executive
chef) rolü açılır. Bu rol, düz "Mutfak Personeli"nin (`V1-IAM-028`)
yapamadığı her şeyi taşır: iptal/sorun bildir (mevcut `orders.send`),
yazdırma kurtarma onay/red (mevcut `kitchen.reprint`), ürün tükendi
bildirimi (`kitchen.availability.suspend`, `V1-KIT-008`). Bu görev,
V1-KIT-008'in ürettiği izin gerçekten var olduktan SONRA çalışır — aksi
halde erişilemeyen bir uca izin vermiş oluruz (bağımsız denetim Y2).

## Owned surface

- `database/migrations/V1/V1-IAM-029/**` (yeni) — `roles` tablosuna
  `kitchen-chef` ("Mutfak Şefi") satırı; `role_permissions`'a `orders.send`,
  `kitchen.advance`, `kitchen.reprint`, `kitchen.availability.suspend`
  atamaları. `kitchen.routing.manage` kasıtlı olarak VERİLMİYOR (Out of
  scope).
- database/MigrationComposition/order.json, src/Host/Composition/
  Migrations/MigrationManifest.cs, tests/Host/MigrationComposition/
  Manifest/ManifestTests.cs (Sınırlı ek, paylaşılan — V1-IAM-028/V1-KIT-008
  emsaliyle aynı desen) — yeni migration pozisyonu 111.
- tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs,
  PermissionSplitDatabaseTests.cs (Sınırlı ek, paylaşılan — V1-IAM-028
  sahipliğinde kalan dosyalar) — 110/111'i up/down zincirine ekler, yeni
  `kitchen-chef` rol/izin testi.
- `docs/domain/authorization-model.md` — yeni §3.2 alt bölümü, "Mutfak
  Şefi"nin taşıdığı/taşımadığı izinleri belgeler (4 FOH rolünün tablosu bir
  sütun olarak genişletilmedi — `kitchen-chef` o tabloya uymuyor, tıpkı
  §3.1'in `kitchen-staff`'ı ayrı bir düzyazı bölümünde ele alması gibi).

## Out of scope

- Yazıcı/rota yönetimi izninin (`kitchen.routing.manage`) Mutfak Şefi'ne
  verilip verilmeyeceği — gereksinim dokümanında "muhtemelen ✅" olarak
  not edilmiş ama kesinleşmemiş açık bir soru; bu görev bunu içermez,
  Semih netleştirince ayrı bir migration/karar olarak eklenir.
- Frontend rol seçici/oturum açma akışına "Mutfak Şefi" seçeneğinin
  eklenmesi (`V1-KDS-002`).

## Dependencies

- V1-KIT-008

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → **0 uyarı, 0 hata** (doğrulandı).
- Migration'ın ileri/geri (up/down) ikisi de boş veritabanında denendi:
  `PermissionSplitDatabase`/`PermissionSplitDatabaseTests` fixture'ı gerçek,
  atılabilir bir Postgres veritabanında hem up-zincirini (111'e kadar) hem
  `ApplyDownSplitAsync`'i (111'den 043'e kadar geriye) çalıştırıyor.
- `dotnet test tests/Modules/Identity/Authorization/ALKAROS.Identity.
  Authorization.Tests.csproj` → **200/200 yeşil** (1 yeni test:
  `KitchenChefRoleExistsAndHoldsCancelReprintSuspendAndAdvance` — rolün tam
  olarak `orders.send`, `kitchen.advance`, `kitchen.reprint`,
  `kitchen.availability.suspend`'i taşıdığını ve `kitchen.routing.manage`'i
  TAŞIMADIĞINI doğruluyor; down-migration testi de `kitchen-chef` rolünün
  ve `kitchen.availability.suspend`'in kaybolduğunu doğrulayacak şekilde
  genişletildi).
- `dotnet test tests/Host/MigrationComposition` → **135/135 yeşil**
  (ManifestTests migration 111'i kapsayacak şekilde güncellendi).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (doğrulandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı).
- Semih'in elle deneyebileceği senaryo: "Mutfak Şefi" (`kitchen-chef`)
  rolüne atanmış bir kullanıcıyla oturum aç, hem bir kalemi iptal et
  (`orders.send`) hem bir ürünü 86'la (`kitchen.availability.suspend`),
  ikisinin de 200 döndüğünü doğrula; aynı oturumla yazıcı rotası
  değiştirmeyi dene ve `kitchen.routing.manage` olmadığı için 403 aldığını
  doğrula.

## Handoff

- None
