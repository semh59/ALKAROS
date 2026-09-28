# V1-RMD-401 - Ödeme alma iznini (`payments.take`) eklemek ve tahsilat uçlarına bağlamak

- Task ID: V1-RMD-401
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi (Q-01): kartla/EFT ile tahsilat ucu yalnız oturum istiyor; yetki modelinde ödeme alma izni hiç
yok. Semih'in kararı (2026-09-28): "bu sistem tarafında ayarlanabilir olsun, bugün kasiyer ödeme alsın diye başlar
yarın garsona POS verilir onunla ödeme al deriz." Buna göre yeni bir izin kodu `payments.take` ("Ödeme alma")
eklenir; varsayılan olarak `cashier`, `supervisor` ve `manager` rollerinde bulunur, `waiter` rolünde bulunmaz. İşletme
garsona el terminali verdiğinde bu izni mevcut rol yönetimi uçlarıyla
(`POST /api/v1/management/roles/roles/{roleId}/permissions`) garson rolüne ekler; kod değişikliği gerekmez.
Tahsilat uçları (kart/EFT tahsilatı ve nakit tahsilat) bu izni ister; nakit tahsilat ayrıca `cash.drawer`
(V1-RMD-400) ister, çünkü para çekmeceye girer.

## Owned surface

- `plan/v1/remediation/V1-RMD-401-payment-taking-permission.md`
- `evidence/V1-RMD-401/**`
- `database/migrations/V1/V1-RMD-401/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json (yalnız 163 girdisi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (yalnız `PhaseBMax` ve doc-comment)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  (yalnız 163 için sayım ve kimlik listesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  (V1-IAM-017 sahipliğinde) — yalnız `PaymentsTake`; kod listesi ve kasiyer katmanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs
  (yalnız yeni kodun sayımı ve rol kapsamı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs
  (yalnız 163 up/down)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (V13-PUI-001 sahipliğinde) — yalnız tahsilat (`POST .../tenders`) ucunun izin kontrolü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs
  (V13-CSH-004 sahipliğinde) — yalnız `cash-tender` ucuna `payments.take` kontrolü
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  (V13-PUI-001 sahipliğinde) — test kasiyerine `payments.take`; izinsiz tahsilat testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  (V13-CSH-004 sahipliğinde) — test kasiyerine `payments.take`; izinsiz nakit tahsilat testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/authorization-model.md (V1-IAM-016 sahipliğinde) — yalnız
  §2 kod tablosuna ve §3 rol matrisine `payments.take` satırı ve karar notu

## In scope

- Migration 163: `payments.take` izni; `cashier`, `supervisor`, `manager` rollerine atama; down geri alır.
- `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/tenders`: `payments.take`.
- `POST .../cash-sessions/{id}/cash-tender`: `cash.drawer` + `payments.take`.
- Ret 403 `FORBIDDEN`, Türkçe mesaj.

## Out of scope

- Rol/izin yönetimi arayüzü (V1-RMD-296 yönetim alanı kararı kapsamında).
- Garson istemcisine (WaiterPwa) ödeme alma ekranı.
- Elle kart onayı uçları (`reconciliation.manage` ile zaten korunuyor).

## Dependencies

- V1-RMD-400

## Acceptance evidence

- Görev kapanışında bu bölüm gerçek koşu çıktılarıyla doldurulur.

## Handoff

- None
