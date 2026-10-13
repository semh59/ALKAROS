# V1-RMD-506 - Gerekçeli hesap iptali ve iptalden sonra hesabın yeniden oluşturulabilmesi

- Task ID: V1-RMD-506
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

PO kararı (2026-10-13): kasiyer ve müdür ödenmemiş hesabı gerekçe yazarak iptal edebilir. Bugün `Bill.Cancel()` hiçbir yerden çağrılmaz; tek iptal yolu siparişi garsona geri çağırmadır ve o yol hesabı ham SQL ile iptal eder. İptal edilen hesabın `bill_number` ve satırlarının `order_item_id` değerleri benzersiz kısıtta kaldığı için aynı siparişten yeni hesap açılamaz (`bills_bill_number_key` çakışması). Bu görev iptali gerekçeli, denetim kayıtlı ve geri dönülebilir (yeni hesap açılabilir) yapar.

## Owned surface

- `plan/v1/remediation/V1-RMD-506-bill-cancellation-and-recreate.md`
- `evidence/V1-RMD-506/**`
- `database/migrations/V1/V1-RMD-506/**`
- `src/Host/Experience/Billing/BillCancellation.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillCancellationHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillFoundation/PostgresBillRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitApplication.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitStore.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/models.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Migration: `bills.bill_number` ve `bill_items.order_item_id` benzersizliği yalnız aktif (iptal edilmemiş) kayıtlar için geçerli olur; iptal gerekçesi ve iptal eden kullanıcı kolonları; yeni `bills.cancel` izni kasiyer, süpervizör ve müdüre atanır.
- Gerekçeli iptal ucu: ödeme ayırması veya onaylı ödemesi olmayan hesap iptal edilir (gerekçe zorunlu); ödeme varsa 409 ve "önce ödeme iade edilmeli" mesajı. Hesap kilidi altında, toplam kayıtlı denetim olayıyla, masa işaretçisi temizlenerek.
- Siparişi garsona geri çağırma yolu da aynı kısıt düzeltmesinden yararlanır: geri çağırma, düzenleme ve yeniden gönderme sonrası kasa yeni hesabı açabilir.
- Gerçek Host + Postgres testi: iptal, yeniden hesap oluşturma, ödemeli hesapta ret, yetkisiz rol ret.

## Out of scope

- Ödenmiş hesabın iadesi ve iptali (V1-RMD-507..509); hesabın arayüz düğmesi (V1-RMD-509); iptal edilmiş hesabın aynı kayıtla yeniden açılması (yeni hesap oluşturma bunun yerine geçer).

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-506/` altındadır.

## Handoff

- None
