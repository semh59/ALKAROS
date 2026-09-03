# V1-RMD-100 - Reserve action Cashier-role scope

- Task ID: V1-RMD-100
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`design_handoff_alkaros_v1` A maddesi: PosTerminal ve Host, boş/dolu masalar için
personel-tetiklemeli `Reserve` komutunu rol ayrımı olmadan sunuyor. Onaylı ürün
kararı (Semih, 2026-09-03): masayı elle yalnız **Cashier** rolü rezerve edebilir;
Waiter edemez; müşteri oturursa sipariş alınır. Bu görev `Reserve` yüzeyini bu role
kapatır ve iki governance dokümanının eski "personel rezervasyon yapamaz" ifadesini
bu kararla uzlaştırır.

## Owned surface

- `plan/v1/remediation/V1-RMD-100-reserve-action-cashier-role-scope.md`
- `docs/domain/table-reservation-policy.md`
- `plan/v1/table-management/V1-TBL-004-table-reservation-record.md`
- `src/Host/Experience/Tables/TableManagementContracts.cs`
- `src/Host/Experience/Tables/TableManagementApplication.cs`
- `src/Clients/PosTerminal/src/features/tables/models.ts`
- `src/Clients/PosTerminal/src/features/tables/tableApi.ts`
- `src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.tsx`
- `src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx`
- `src/Clients/PosTerminal/src/strings.ts`
- `tests/Host/Experience/Tables/**`
- `tests/Clients/PosTerminal/**` (yalnız tablo rezervasyon testleri)
- `evidence/V1-RMD-100/**`
- Yüzey devirleri: `TableManagementContracts.cs` / `TableManagementApplication.cs`
  custody'si mevcut sahibi `V1-RMD-013`'ten; `features/tables/**` custody'si
  `V1-RMD-017` / `V1-RMD-028`'den; `strings.ts` custody'si `V1-RMD-068`
  yüzeyinden bu göreve geçer. İlgili historical görevler `Done` kalır.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- `docs/domain/table-reservation-policy.md`: "Who creates `Reserved`" satırı ve
  "Rejected alternatives" listesi, onaylı karara (`PO:2026-09-03`) göre güncellenir:
  QR order state machine + **Cashier rolü** elle rezervasyon oluşturabilir; Waiter
  ve diğer roller oluşturamaz. Karar kaydı formatı korunur (approver, tarih, neden).
- `V1-TBL-004`: "Out of scope: Rezervasyon UI beklemede" satırı, Cashier-only
  rezervasyon UI'sinin V1'de kapsamda olduğunu yansıtacak biçimde düzeltilir;
  `V1-TBL-004` `Done` kalır, yalnız kapsam metni uzlaştırılır.
- `TableManagementContracts.cs` `AllowedCommands`: `Reserve`, yalnız aktörün Cashier
  yetkisi/capability'si varsa listeye eklenir (imza actor rol/capability alacak
  şekilde genişletilir; `CancelReservation` / `ClaimReservation` mevcut davranışta
  kalır — bunlar var olan bir rezervasyonu yönetir).
- `TableManagementApplication.cs` `POST /reservations`: Cashier capability
  doğrulaması (yoksa `403` typed sonuç). QR akışı (V1.4) için endpoint korunur.
- PosTerminal `features/tables`: `Reserve` aksiyonu yalnız sunucudan gelen
  `allowedCommands` içinde varsa render edilir (mevcut desen); union ve
  `actionNeedsReason` değişmeden kalır, davranış değişikliği sunucu tarafında.
- `strings.ts`: `tableActionLabels.Reserve` etiketi korunur (Cashier hâlâ görür).
- Testler: Cashier `Reserve` görür + endpoint `201`; Waiter/diğer rol `Reserve`
  görmez + endpoint `403`; `CancelReservation`/`ClaimReservation` her iki rolde
  regresyona uğramaz.

## Out of scope

- QR order state machine (`V14-QRO-002` / `V14-QRO-003`) — rezervasyonun ikinci
  yaratıcısı; bu görev yalnız V1 personel yolunu rol-gate eder.
- `src/Modules/Tables/Reservations/**` kayıt/projeksiyon katmanı (`V1-TBL-007`
  yüzeyi) — davranışı değişmez.
- Yeni capability/rol modeli tanımlamak; mevcut `LoginResponse.capabilities` /
  IAM yetki mekanizması (`V1-IAM-002`) kullanılır.
- WaiterPwa — zaten `Reserve` aksiyonu yok; dokunulmaz.

## Dependencies

- V1-TBL-004
- V1-TBL-007
- V1-IAM-002
- V1-RMD-013
- V1-RMD-017

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release --no-restore` 0 uyarı / 0 hata;
  `dotnet test` tablo yönetimi filtresinde yeni testler geçer (Cashier görür/`201`,
  Waiter görmez/`403`, iptal/sahiplen regresyonsuz);
  `pnpm --dir src/Clients/PosTerminal typecheck && ... test && ... build` exit 0.
- Migration yok.
- Semih: Cashier rolüyle giriş yapar, salon planında boş bir masada "Rezervasyon
  al" görür ve rezervasyon oluşturur; Waiter rolüyle giriş yapıp aynı masada bu
  aksiyonun görünmediğini doğrular; `docs/domain/table-reservation-policy.md`
  içindeki güncel kararı ve onay satırını okur.

## Handoff

- V14-QRO-003
