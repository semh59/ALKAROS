# V1-IAM-024 - Endpoint re-pointing and alias removal

- Task ID: V1-IAM-024
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`RequireMutationAsync` ile tek `MutationPermission` çağıran her Experience
endpoint'ini kendi granüler koduna bağlar (Tables, Orders, Billing, Cash);
`AllowedCommands` sonucunu, çağıranın sahip olduğu izinler ile ulaşılabilir
yetki isteği eylemlerinin birleşimi olarak hesaplar; böylece POS istemcisi bir
eylemi gizlemek yerine onay gerektiren biçimde gösterir. Eski `pos.cashier.mutate`
takma adını ve kodunu kaldırır.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-024-endpoint-repointing-and-alias-removal.md`
- `database/migrations/V1/V1-IAM-024/**`
- `src/Modules/Identity/Authorization/024/**`
- `tests/Modules/Identity/Authorization/024/**`
- `evidence/V1-IAM-024/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- Şu dosyalarda tek satırlık izin kodu eşlemesi: `TableManagementContracts.cs`
  ile `TableManagementApplication.cs` (`AllowedCommands` ve `MutationPermission`
  çağrıları), `DualScreenApplication.cs` (`CashierMutationPermission` sabiti),
  ilgili Orders, Billing ve Cash Experience endpoint'leri, PosTerminal
  `features/tables` istemci süzgeci.
- `pos.cashier.mutate` izin satırının ve sabitinin kaldırılması.
- `AllowedCommands` sonucunun "sahip olunan izinler ile ulaşılabilir yetki
  istekleri" birleşimine dönüştürülmesi.

## Out of scope

- Politika, yetki isteği, devir, çevrimdışı bütçe ile davranışsal sıkılaştırma
  davranışı; bunlar `V1-IAM-018` ile `V1-IAM-023` arasındadır.
- Yeni iş davranışı; yalnız izin kodu eşleme ile takma ad kaldırma yapılır.

## Dependencies

- V1-IAM-017
- V1-IAM-018
- V1-IAM-019
- V1-IAM-020
- V1-IAM-021
- V1-IAM-022
- V1-IAM-023

## Blocker

- Bu görev yürütmeye alınmadan önce, `In scope` altında sayılan mevcut sahipli
  dosyalar için custody devri kayıtları ilgili görevlere eklenmelidir:
  `TableManagementContracts.cs` ve `TableManagementApplication.cs` için
  `V1-RMD-013` ile `V1-RMD-026`; `DualScreenApplication.cs` için `V1-RMD-098`;
  `src/Clients/PosTerminal/src/features/tables/**` için `V1-RMD-017` ile
  `V1-RMD-071`; Orders için `V1-RMD-050` ve `V1-RMD-066`; Billing için
  `V1-RMD-054` ve `V1-RMD-057`. Ancak bu custody devri kayıtları `V1-RMD-098`
  deseniyle eklenip `plan/AUDIT_MANIFEST.json` yeniden üretildiğinde ve
  `validate` ile `verify-manifest` temiz kaldığında görev yeniden `Planned`
  yapılabilir.

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; tam
  `dotnet test` paketi geçer; `pos.cashier.mutate` dizgisi `src` ile `tests`
  altında kalmaz.
- Migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: `waiter` rolünde giriş yapılır ve salon planında
  void düğmesi onay gerektiren etiketiyle görünür ama doğrudan çalışmaz;
  `cashier` rolünde rezervasyon yapılabilir; `supervisor` rolünde void ile ikram
  doğrudan çalışır.

## Handoff

- V14-QRO-003
