# V1-RMD-868 - Billing ve Kitchen projeleri Npgsql paketine doğrudan bağlanır

- Task ID: V1-RMD-868
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9/F0082 ve aynı hatanın ikinci örneği: src/Modules/Billing ve src/Modules/Kitchen kodu `using Npgsql` ile Npgsql tipleri kullanır (PostgresBillRepository, PostgresBillTerminalOwnerRepository, TableEventBillConsumer, Kitchen depoları) ama proje dosyalarında Npgsql PackageReference'ı yoktur; derleme yalnız başka bir projenin (Orders) geçişli bağımlılığı sayesinde çalışır. Orders paketi bırakırsa ya da sürümü ayrışırsa bu iki modül sessizce kırılır ya da yanlış sürümle derlenir. Diğer tüm src projelerinde (taranarak doğrulandı) kullanan proje paketi doğrudan bildirir. İki projeye merkezi sürüm yönetimiyle `PackageReference Include="Npgsql"` eklenir; kilit dosyaları yeniden üretilir.

## Owned surface

- `evidence/V1-RMD-868/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Billing/ALKAROS.Billing.csproj — Npgsql doğrudan referansı.
  - src/Modules/Billing/packages.lock.json — yeniden üretilen kilit.
  - src/Modules/Kitchen/ALKAROS.Kitchen.csproj — Npgsql doğrudan referansı.
  - src/Modules/Kitchen/packages.lock.json — yeniden üretilen kilit.
  - src/Host/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - src/Modules/Billing/PaymentClosure/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - src/Modules/Cash/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - src/Modules/CustomerAccounts/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - src/Modules/Payments/TenderComposition/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - src/Modules/Payments/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Architecture/ApiConventions/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Architecture/ModuleBoundaries/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Errors/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Authorization/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Billing/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/CashSession/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Catalog/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Composition/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/CustomerAccounts/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/HelpRequests/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Inventory/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/InventoryReporting/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/KitchenOperations/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Menu/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/NfcOrdering/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Observability/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/OfflineReconciliation/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/OnlineOrdering/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Orders/Comp/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Orders/Confirmation/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Orders/TableDraft/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Orders/Void/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Orders/VoidSent/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/PaymentTender/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/PendingOrderNotifications/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Production/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Purchasing/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/QnbCredentialSettings/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/QrOrdering/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Recipes/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Reconciliation/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/RelaySettings/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Reporting/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Roles/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/SecurityAdministration/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Settings/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/Tables/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/TokenTerminalSettings/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/WaiterNotifications/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Experience/WebPush/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/MigrationComposition/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/AnalysisRunner/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/BrowserErrors/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/CrashChannel/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/DomainDetectors/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/ErrorEventEndpoints/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/HealthSnapshot/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/InsightRunner/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/InteractionTelemetry/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/JourneyEndpoints/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/MonitoringSwitch/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/NotificationWiring/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/OfflineQueueReport/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/OutageWatchdog/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/ResourceProbes/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/SecurityDetectors/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/ServerDetectors/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/ServerDetectorsDeep/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/ServerEndToEnd/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/SyntheticProbes/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/TraceCorrelation/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Host/Observability/Wiring/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Billing/Adjustments/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Billing/BillFoundation/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Billing/PaymentClosure/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Billing/SplitDesign/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Cash/Contracts/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Cash/SessionLifecycle/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Cash/TenderHandler/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Cash/TransactionLedger/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/CustomerAccounts/AccountPayments/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/CustomerAccounts/AccountReceipts/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/CustomerAccounts/BalanceProjection/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/CustomerAccounts/BillCharges/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/CustomerAccounts/CashReceipts/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/CustomerAccounts/TransactionLedger/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Kitchen/OrderItemStateSync/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Kitchen/PhysicalPrintRecovery/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Kitchen/PrintQueue/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Kitchen/Routing/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Kitchen/TicketLifecycle/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Payments/Allocations/Persistence/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Payments/Allocations/RefundIntents/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Payments/CardSettlement/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Payments/EftTender/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Payments/PaymentAggregate/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Payments/TenderComposition/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Payments/TenderRouting/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Payments/Token/TerminalCredential/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Reconciliation/Payments/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Tables/CurrentPointers/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Tables/Reservations/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Tables/TableMerge/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
  - tests/Modules/Tables/TableTransfer/packages.lock.json — projeyi kapsayan kilit dosyası (proje bağımlılık listesi Npgsql ile yenilenir).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. İki PackageReference (sürüm Directory.Packages.props'tan gelir, yeni sürüm eklenmez).
2. Kilit dosyalarının `dotnet restore --force-evaluate` ile yeniden üretilmesi. Düzeltme: Billing ve Kitchen'e bağımlı bütün projelerin kilit dosyası da değişir (toplam 102), çünkü kilitler proje bağımlılıklarının paketlerini de listeler; kilitli geri yükleme (NU1004) aksi halde bozulur. Bütününü `dotnet restore ALKAROS.slnx --force-evaluate` üretir.
3. Kanıt: önce/sonra `dotnet list package --include-transitive` çıktısında Npgsql'in "Top-level" olduğu; Billing ve Kitchen derlemesi ve Billing testleri exit 0.

## Out of scope

- Paket sürümü yükseltmek; başka proje dosyaları.

## Dependencies

- None

## Acceptance evidence

1. Billing ve Kitchen projeleri derlenir (exit 0), ilgili modül testleri geçer; çıktılar `evidence/V1-RMD-868/`.
2. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
