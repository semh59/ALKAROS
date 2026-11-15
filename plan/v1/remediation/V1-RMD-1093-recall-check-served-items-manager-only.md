# V1-RMD-1093 - Servis edilmiş ürünlü hesabı masaya geri göndermeyi yalnız yetkili yönetici yapar

- Task ID: V1-RMD-1093
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-15: kasa kuyruğundaki hesapta servis edilmiş ürün varsa "Masaya geri gönder" yalnız yetkili yöneticiye (payments.refund.approve) açıktır. Bugün recall-from-cashier ucu yalnız orders.create ister ve hesabı servis edilmiş kalemi denetlemeden iptal eder; oysa hesap iptalinde sunucu aynı durumu 409 BILL_HAS_SERVED_ITEMS ile kasiyere yasaklar. Düzeltme: sunucu servis edilmiş etkin kalem varsa ve çağıranda payments.refund.approve yoksa 409 CHECK_HAS_SERVED_ITEMS döner ve hiçbir şeyi değiştirmez; kasa kuyruğu listesi hesap başına servis edilmiş kalem sayısını verir; kasa kuyruğu ekranı bu hesapta kasiyere düğme yerine "Hesapta servis edilmiş ürün var; masaya yalnız yetkili yönetici geri gönderebilir. Yetkili yöneticiye başvurun." notunu gösterir (V1-RMD-1089 deseni). Yetkili yönetici eski davranışı görür. Ek olarak V1-RMD-1089'dan kalan test boşluğu kapanır: workspace.tsx içindeki payments.refund.approve yetkisinin masa panelindeki canCancelServed ve kasa kuyruğundaki yeni bayrağa bağlandığı workspace.test.tsx içinde sınanır.

## Owned surface

- `evidence/V1-RMD-1093/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/CashierHandoffStore.cs
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs
  - src/Host/Experience/Orders/OrderManagementContracts.cs
  - tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs
  - src/Clients/PosTerminal/src/features/pending-checks/pendingChecksApi.ts
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.cancel.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Garson PWA geri alma düğmesi (sunucunun Türkçe iletisini zaten gösterir); hesap iptal kuralı; kasa kuyruğundaki sipariş iptali

## Dependencies

- V1-RMD-1092

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
