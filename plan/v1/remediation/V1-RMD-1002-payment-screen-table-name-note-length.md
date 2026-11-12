# V1-RMD-1002 - Ödeme ekranı masa adı, kasada eklendi notu ve kısa ilk ekran

- Task ID: V1-RMD-1002
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Ödeme, bölme ve Bekleyen hesaplar ekranlarında ve fişte masa adı görünür; mutfağa gitmeden eklenen kalemler bu ekranlarda ve fişte kasada eklendi notuyla işaretlenir; ödeme ekranında Ödemeyi Ekle ilk ekranda görünür (ekran kısaltılır).

## Owned surface

- `evidence/V1-RMD-1002/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitContracts.cs
  - src/Host/Experience/Billing/BillingSplitStore.cs
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs
  - src/Host/Experience/Orders/CashierHandoffStore.cs
  - src/Host/Experience/Orders/OrderManagementContracts.cs
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - tests/Host/Experience/Billing/MergedBillOrderLinkTests.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/billing/models.ts
  - src/Clients/PosTerminal/src/features/billing/billing.css
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.cancel.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/pendingChecksApi.ts
  - src/Clients/PosTerminal/src/features/pending-checks/pending-checks.css
  - src/Clients/PosTerminal/src/strings.ts
  - tests/Clients/StaticApps/split-payment-receipt.test.js
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js
  - tests/Clients/StaticApps/split-payment-table-note.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Üçüncü deneme #4: ödeme ekranında masa adı.
2. Üçüncü deneme #5: kasada eklendi notu ödeme, bölme, Bekleyen hesaplar ekranında ve fişte.
3. Üçüncü deneme #7: 768x1024 ve 1366x768 çözünürlükte Ödemeyi Ekle kaydırmadan görünür.

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Koyu tema

## Dependencies

- V1-RMD-1001

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları (istemci değişiyorsa)
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None
