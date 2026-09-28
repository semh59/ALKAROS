# V1-RMD-394 - CI kapısını yeniden çalışır hale getir (kilitli restore, Windows Python, manifest)

- Task ID: V1-RMD-394
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

`production-validation` iş akışını, kod dışı nedenlerle hiçbir adımı çalıştıramadan düşmekten çıkarmak:
(1) V14-CST-001/V14-ACC-001'in eklediği proje referanslarını 39 kilit dosyasına yansıtarak
`dotnet restore --locked-mode`'u geçirmek, (2) Python 3.12.12'nin Windows derlemesi olmadığı için
`Task scope enforcement` job'unu Ubuntu runner'a taşımak, (3) master'da 1 bayt sapan audit manifestini
aracın kendisiyle yeniden üretmek. V1-RMD-393 F-01/F-02'nin düzeltmesidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-394-ci-gate-restore.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  .github/workflows/task-scope.yml
  plan/AUDIT_MANIFEST.json
  plan/AUDIT_REPORT.md
  tests/Host/Experience/Authorization/packages.lock.json
  tests/Host/Experience/Billing/packages.lock.json
  tests/Host/Experience/CashSession/packages.lock.json
  tests/Host/Experience/Catalog/packages.lock.json
  tests/Host/Experience/Composition/packages.lock.json
  tests/Host/Experience/HelpRequests/packages.lock.json
  tests/Host/Experience/Inventory/packages.lock.json
  tests/Host/Experience/InventoryReporting/packages.lock.json
  tests/Host/Experience/KitchenOperations/packages.lock.json
  tests/Host/Experience/Menu/packages.lock.json
  tests/Host/Experience/NfcOrdering/packages.lock.json
  tests/Host/Experience/Observability/packages.lock.json
  tests/Host/Experience/OfflineReconciliation/packages.lock.json
  tests/Host/Experience/OnlineOrdering/packages.lock.json
  tests/Host/Experience/Orders/Comp/packages.lock.json
  tests/Host/Experience/Orders/Confirmation/packages.lock.json
  tests/Host/Experience/Orders/TableDraft/packages.lock.json
  tests/Host/Experience/Orders/Void/packages.lock.json
  tests/Host/Experience/Orders/VoidSent/packages.lock.json
  tests/Host/Experience/PaymentTender/packages.lock.json
  tests/Host/Experience/PendingOrderNotifications/packages.lock.json
  tests/Host/Experience/Production/packages.lock.json
  tests/Host/Experience/Purchasing/packages.lock.json
  tests/Host/Experience/QnbCredentialSettings/packages.lock.json
  tests/Host/Experience/QrOrdering/packages.lock.json
  tests/Host/Experience/Recipes/packages.lock.json
  tests/Host/Experience/Reconciliation/packages.lock.json
  tests/Host/Experience/RelaySettings/packages.lock.json
  tests/Host/Experience/Reporting/packages.lock.json
  tests/Host/Experience/Roles/packages.lock.json
  tests/Host/Experience/SecurityAdministration/packages.lock.json
  tests/Host/Experience/Settings/packages.lock.json
  tests/Host/Experience/Tables/packages.lock.json
  tests/Host/Experience/TokenTerminalSettings/packages.lock.json
  tests/Host/Experience/WaiterNotifications/packages.lock.json
  tests/Host/Experience/WebPush/packages.lock.json
  tests/Modules/CustomerAccounts/BalanceProjection/packages.lock.json
  tests/Modules/CustomerAccounts/TransactionLedger/packages.lock.json
  tests/Modules/CustomerData/Profiles/packages.lock.json

## In scope

- Kilit dosyaları yalnız `dotnet restore --force-evaluate` ile üretilir; satır sonları mevcut dosyalarla aynı
  (CRLF) tutulur, yalnız gerçekten değişen 39 dosya commit'lenir.
- Enforce job'unda yalnız `runs-on` değişir; adımlar `pwsh` olarak aynı kalır.

## Out of scope

- Master'da kırmızı olan üç test paketi (Production, Composition, CustomerData.AnonymizationState): ayrı görevler.
- Paket sürümü yükseltme, iş akışında başka adım değişikliği.

## Dependencies

- V1-RMD-393

## Acceptance evidence

- Temiz klonda `dotnet restore ALKAROS.slnx --locked-mode` çıkış kodu 0.
- `plan_audit_tool.py validate` ve `verify-manifest` 0 hata.
- GitHub Actions'ta bu dal için `Task scope enforcement` job'u Python kurulumunu geçer ve kapsam doğrulamasını
  çalıştırır; `validate` job'u manifest ve kilitli restore adımlarını geçer (kalan kırmızı adımlar ayrı görevlere
  aittir ve kanıtta listelenir).

## Handoff

- None
