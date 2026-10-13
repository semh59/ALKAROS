# V1-RMD-508 - İade isteği, onayı ve iade sonrası hesabın kapanması

- Task ID: V1-RMD-508
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

PO kararı (2026-10-13): ödenmiş hesap doğrudan iptal edilmez, önce iade edilir. Nakit iade: kasiyer gerekçeyle ister, müdür onaylar, tutar iade eden kasiyerin açık kasa oturumunda iade olarak görünür. Kart iadesi şimdilik elle: sistem "kart iadesi gerekli" kaydı açar, para banka/POS panelinden elle iade edilir, sonuç (fiş numarası dahil) girilir. Hesabın tüm ödemeleri iade edilince hesap iptal olur. Kapsam taslaktır; başlamadan önce Owned surface keşifle netleştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-508-refund-request-approval-and-bill-closure.md`
- `evidence/V1-RMD-508/**`
- `database/migrations/V1/V1-RMD-508/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/PaymentRefunds.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/PaymentRefundHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitApplication.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillCancellation.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/models.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Migration 183 `payments.refund_requests` (ödeme başına tek açık istek; tam iade), migration 184 `payments.refund.request` (kasiyer, süpervizör, müdür) ve `payments.refund.approve` (süpervizör, müdür) izinleri.
- Uçlar: iade iste, bekleyenleri listele, onayla, reddet, elle iadeyi tamamla (fiş numarası). İsteyen ile onaylayan aynı kişi olamaz; gerekçe zorunlu.
- Nakit onayı: iade eden kasiyerin açık kasa oturumuna `Refund` (çıkış) satırı, `PostgresPaymentReversalRepository` ile iade; kart ve diğer yöntemler onayda `AwaitingManualRefund` olur, sonuç girilince iade yazılır.
- Hesabın tüm ödemeleri iade edilince hesap `Cancelled` olur (iptal gerekçesi iade); iptal ucundaki para kontrolü iade edilmiş ödemeleri para saymaz; `payment.refunded` denetim olayı.
- Hesaba (müşteri cari) yazılan ödeme bu görevde iade edilmez (409).

## Out of scope

- Arayüz (V1-RMD-509); kısmi iade; banka/POS entegrasyonuyla otomatik kart iadesi.

## Dependencies

- V1-RMD-507

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-508/` altındadır.

## Handoff

- None
