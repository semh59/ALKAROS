# V1-RMD-1132 - Fiziksel kasa çekmecesi: "çekmeceyi aç" yetkisi ve açma/kapama ile yazıcı adresi ayarları

- Task ID: V1-RMD-1132
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

PO kararı (2026-11-17): fiziksel kasa çekmecesi yazıcı üzerinden açılacak (nakit ödemede kendiliğinden, "Satış yok" ile elle), özellik AYARLA kapatılıp açılabilecek ve varsayılan KAPALI olacak. Bu görev zemini kurar; çekmece açan kod sonraki görevlerdedir (V1-RMD-1133 bayt gönderimi, V1-RMD-1134 uçlar).

1. Yeni yetki `cash.drawer.kick` (para işlemi olmadan çekmeceyi elle açma): mevcut `cash.drawer` kasa oturumu yetkisidir ve fiziksel darbe anlamı taşımaz; bu yüzden ayrı kod. Migration 231 yetkiyi ekler ve cashier, supervisor, manager rollerine verir (waiter almaz); `.down.sql` ikisini geri alır. Katalog (`ApplicationPermissions`) 27 koddan 28 koda çıkar; kasiyer kümesine eklenir; `MigrationSeededCodes` (11) değişmez çünkü yetki katalogda yaşar. Yetki katalog testleri, `PermissionSplitDatabase` elle migration listesi, `order.json`, `MigrationManifest.PhaseBMax` (230 -> 231) ve manifest testleri birlikte güncellenir. Yönetim ve karar ekranlarındaki Türkçe yetki etiketleri eklenir ("Çekmeceyi satışsız açma").
2. İki yönetici ayarı (tipli ayarlar, yeni tablo yok, mevcut `ManagerSelfApprovalSetting` kalıbı): `cash.drawer_kick_enabled` (Toggle, Global, varsayılan `false`) ve `cash.drawer_printer_address` (Text, Global, varsayılan boş; biçim `adres:port`, adres harf/rakam/nokta/tire en çok 253 karakter, port 1-65535; boş değer "yazıcı yok" demektir; hatalı biçim Türkçe cümleyle reddedilir). Ayarlar `EnsureKnownSettingsRegisteredAsync` listesinde kayıtlıdır ve yönetim ayar ekranında Türkçe etiketle görünür. Küçük `CashDrawerSettings` sınıfı "açık mı ve adres geçerli mi" kararını tek yerde verir: ayar açık VE adres geçerliyse çekmece etkindir; aksi halde etkin değildir.
3. Karar ve gerekçe (yazıcı adresi neden ayar): `kitchen.printers` her satırda bir mutfak istasyonu ister, kasa yazıcısı mutfak istasyonu değildir; yazıcı tablosunu genişletmek Kitchen modülünün varlık, depo, sözleşme ve ekranına dokunur. Küçük işletmede çekmece genellikle tek kasa yazıcısındadır; bu yüzden işletme düzeyinde tek ayar yeterlidir. Sınır: terminal başına ayrı çekmece/yazıcı bu görevde YOKTUR (iki kasada iki ayrı çekmece gerekirse yeni görev); risk olarak raporlanır.
4. Anahtar adları gizli-anahtar yasağı kelimelerini (token, secret vb.) içermez.

## Owned surface

- `evidence/V1-RMD-1132/**`
- `database/migrations/V1/V1-RMD-1132/231-cash-drawer-kick-permission.up.sql`
- `database/migrations/V1/V1-RMD-1132/231-cash-drawer-kick-permission.down.sql`
- `src/Modules/Settings/CashDrawer/CashDrawerSettings.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Modules/Settings/TypedSettings/CashDrawerSettingsTests.cs (V1-SET-001) — yeni dosya: ayar testleri.
  - database/MigrationComposition/order.json
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
  - src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  - tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs
  - tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs
  - src/Modules/Settings/TypedSettings/SettingValidator.cs
  - src/Host/Experience/Settings/SettingsManagementEndpoints.cs
  - tests/Host/Experience/Settings/SettingsManagementHttpTests.cs
  - docs/architecture/settings-ownership.md
  - src/Clients/PosTerminal/src/features/authorization-decisions/models.ts
  - src/Clients/PosTerminal/src/features/management-staff-roles/models.ts
  - src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.test.tsx
  - src/Clients/PosTerminal/src/features/management-settings-history/models.ts
  - src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Katalog, migration ileri/geri, ayar doğrulama ve ayar ucu testleri; PosTerminal istemci testleri ve üretim derlemesi

## Out of scope

- Çekmeceye bayt gönderimi (V1-RMD-1133); nakit ödeme kancası, "Satış yok" ucu, kapanış sayacı (V1-RMD-1134); kasa ekranı düğmesi (V1-RMD-1135); ayar ekranında metin düzenleme ve kılavuz (V1-RMD-1136); terminal başına çekmece

## Dependencies

- V1-RMD-1131

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. Migration ileri ve geri kanıtı
4. İstemci üretim derlemesi kanıtı (`clientBuildGate.js`)

## Handoff

- V1-RMD-1133
