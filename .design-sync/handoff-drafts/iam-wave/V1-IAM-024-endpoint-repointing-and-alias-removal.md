# V1-IAM-024 - Endpoint re-pointing and alias removal

- Task ID: V1-IAM-024
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`RequireMutationAsync` / tek `MutationPermission` cagiran her Experience
endpoint'ini kendi granuler koduna (Tables / Orders / Billing / Cash) baglar;
`AllowedCommands` sonucunu "aktorun sahip oldugu izinler BIRLESIM ulasilabilir
grant eylemleri" olarak hesaplar (POS istemcisi "Void (onay gerekir)" gosterir);
`pos.cashier.mutate` gecis takma adini ve kodunu kaldirir.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-024-endpoint-repointing-and-alias-removal.md`
- `database/migrations/V1/V1-IAM-024/**`
- `src/Host/Experience/Tables/TableManagementContracts.cs`
- `src/Host/Experience/Tables/TableManagementApplication.cs`
- `src/Host/Experience/Orders/**`
- `src/Host/Experience/Billing/**`
- `src/Host/Experience/Cash/**`
- `src/Host/DualScreen/DualScreenApplication.cs`
- `src/Clients/PosTerminal/src/features/tables/**`
- `tests/Host/Experience/**`
- `evidence/V1-IAM-024/**`
- Yuzey devirleri: `TableManagement*.cs` custody'si `V1-RMD-013` / `V1-RMD-026`;
  `DualScreenApplication.cs` `V1-RMD-098`; `features/tables/**` `V1-RMD-017` /
  `V1-RMD-071`; ilgili Experience Host dosyalari mevcut sahiplerinden bu goreve
  gecer. Historical gorevler `Done` kalir.
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## Out of scope

- Politika/grant/devir/butce/sikilastirma davranisi (V1-IAM-018..023).
- Yeni is davranisi; yalniz izin kodu esleme ve alias kaldirma.

## Dependencies

- V1-IAM-017
- V1-IAM-018
- V1-IAM-019
- V1-IAM-020
- V1-IAM-021
- V1-IAM-022
- V1-IAM-023

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` 0 uyari / 0 hata; `dotnet test` tam
  paket gecer; `pos.cashier.mutate` string'i kod tabaninda kalmaz.
- Migration ileri/geri bos veritabaninda denenir.
- Semih: `waiter` rolunde giris yapar; salon planinda "Void" butonunu "onay
  gerekir" etiketiyle gorur ama dogrudan calistiramaz; `cashier` rolunde
  rezervasyon yapabilir; `supervisor` rolunde void/comp'u dogrudan yapar.

## Handoff

- V14-QRO-003
