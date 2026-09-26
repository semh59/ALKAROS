# V1-RMD-339 - Onaylanan yeniden basımlar artık gerçekten yazıcıya ulaşıyor

- Task ID: V1-RMD-339
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "`PhysicalPrintRecoveryService`'in ölü kod
yüzeyi bilinenden geniş: 4 metod hiç çağrılmıyor" (`PhysicalPrintRecoveryService.cs:106-160`). Doğrulandı ve
denetimin işaret ettiğinden daha ciddi bir gerçek işlevsel boşluk bulundu:

- `KitchenOperationsStore.ApproveReprintAsync`/`RejectReprintAsync`, `IPhysicalPrintRecoveryService`'i hiç
  çağırmadan, `_deliveries` (repository) üzerinde AYNI domain geçişini (`delivery.ApproveReprint`/
  `RejectReprint`) yinelenmiş şekilde doğrudan yapıyordu — servisin `ApproveOperatorReprintAsync`/
  `RejectOperatorReprintAsync` metotları bu yüzden hiç çağrılmıyordu (kod yinelemesi).
- Daha önemlisi: `ExecuteApprovedReprintAsync` — eşzamanlı yürütmeye karşı fencing, aktarım hatasında teslimatı
  otomatik olarak `Unknown`'a geri döndürme gibi GERÇEK güvenlik mantığı içeren TEK metot — hiçbir yerden hiç
  çağrılmıyordu. Sonuç: bir yönetici bir yeniden basımı onayladığında teslimat `ReprintApproved` durumuna geçiyor
  ve SONSUZA KADAR orada kalıyordu — hiçbir kod yolu asla fiziksel yazıcıya gerçekten gönderim yapmıyordu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/KitchenOperations/KitchenOperationsTestDatabase.cs
- `plan/v1/remediation/V1-RMD-339-physical-print-recovery-reprint-execution-wired.md`

## In scope

1. `KitchenOperationsStore.ApproveReprintAsync`: artık `IPhysicalPrintRecoveryService.ApproveOperatorReprintAsync`'i
   çağırıyor (kod yinelemesi kaldırıldı), ardından hemen `ExecuteApprovedReprintAsync`'i gerçek
   `IPrinterTransport` ile çağırarak fiziksel yeniden basımı GERÇEKTEN gerçekleştiriyor. `RejectReprintAsync` da
   aynı şekilde `RejectOperatorReprintAsync`'e yönlendirildi.
2. Yazıcıya ulaşılamazsa (`PrinterUnreachableException`/`PrinterTransmissionUncertainException`): yeni bir 503
   `PRINTER_UNREACHABLE` Türkçe hatası döner; teslimat, domain'in kendi kendini onaran geçişiyle
   (`MarkReprintUnknown`) otomatik olarak `Unknown`'a geri döner — bir yönetici basitçe tekrar onaylayarak yeniden
   deneyebilir, kayıt kalıcı olarak takılıp kalmıyor.
3. `KitchenOperationsStore`'un yapıcısına `IPhysicalPrintRecoveryService`/`IPrinterTransport` eklendi (her ikisi
   de zaten `AddKitchenOperationsExperience`'ta kayıtlı — DI değişikliği gerekmedi).

## Out of scope

1. `ConfirmDeliverySuccessAsync` — hâlâ hiçbir çağrı yeri yok, ama bu KASITLI: normal (sorunsuz) bir basım hiçbir
   zaman bir `PhysicalPrintDelivery` kaydı oluşturmuyor (`KitchenPrintDispatchHostedService.ExecuteDeliveryAsync`
   yalnızca belirsiz/çökme-penceresi durumunda bir kayıt açıyor), bu yüzden bu metodun gerçekten çağrılabileceği
   bir senaryo şu an sistemde yok. Gelecekte bir operatörün gözlemlediği başarılı bir teslimatı manuel
   onaylaması gibi bir özellik eklenirse gerekli olacak — bu görevin isim verdiği "en kritik" boşluk
   (`ExecuteApprovedReprintAsync`) değil.
2. `IPhysicalPrintRecoveryService`/`IPrinterTransport`'un aynı deseni tekrarlayan başka çağrı siteleri —
   grep ile doğrulandı, tek çağıran `KitchenPrintDispatchHostedService` (değişmedi) ve şimdi
   `KitchenOperationsStore`.

## Dependencies

- None

## Acceptance evidence

- `tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj`: 36/36 test
  geçti (2 yeni test dahil).
- `UnknownDeliveryRequiresReasonedReprintApprovalAndTheApprovalActuallyReprintsIt`: gerçek bir loopback TCP
  dinleyicisi (`TcpEscPosPrinterTransportTests`'in kendi kalıbı) kullanılarak yeniden basımın GERÇEKTEN bir
  soket'e ulaştığı, gönderilen baytların operatörün onay nedenini (`"NEDEN: {reason}"`) içerdiği ve teslimatın
  hem API yanıtında hem veritabanında `Reprinted` durumuna ulaştığı doğrulandı.
- `WhenThePrinterCannotBeReachedTheApprovalReportsItAndTheDeliveryGoesBackToUnknown`: dinlenmeyen bir porta karşı
  onay isteği 503 döndürüyor VE teslimat veritabanında `Unknown`'a geri dönüyor.
- Mutasyon kontrolü: `KitchenOperationsStore.cs` eski (yinelenmiş, yürütmesiz) haline döndürüldü, her iki yeni
  test beklenen şekilde kırmızıya döndü (`Expected: "Reprinted", Actual: "ReprintApproved"` ve `Expected:
  ServiceUnavailable, Actual: OK` — eski kodun yazıcıya hiç ulaşmadan sessizce 200 döndürdüğünü kanıtlıyor).
  Dosya geri yüklendi, yeniden derleme sonrası paket tekrar 36/36 yeşile döndü.
- `ALKAROS.Host.csproj`: sıfır hata, sıfır uyarı ile derlendi.

## Handoff

- None
