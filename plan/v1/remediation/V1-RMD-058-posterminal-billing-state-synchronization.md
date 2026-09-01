# V1-RMD-058 - PosTerminal billing state synchronization

- Task ID: V1-RMD-058
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`PosTerminal/src/App.tsx` içindeki `BillingRoute` bileşeninde siparişten adisyona geçiş sırasında dönen `billId`'nin yerel state ile senkronize edilerek sonraki bölme ve temizleme işlemlerinin doğru adisyon üzerinde çalışmasını sağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-058-posterminal-billing-state-synchronization.md`
- PO:2026-08-31 kararıyla src/Clients/PosTerminal/src/App.tsx yüzeyi V1-RMD-075'e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-058/**`

## In scope

- `App.tsx` içindeki `BillingRoute` bileşeninde `createFromOrder` çağrısından sonra dönen `billId`'nin state'e yazılması ve `useMemo`/`client` ile senkronize edilmesi.
- `searchParams` değişimlerinde `billId` state'inin tutarlı yönetilmesi.

## Out of scope

- Backend host dosyalarını değiştirmek.

## Dependencies

- V1-GOV-034

## Deliverables

- State kilitlenmesi olmayan ve adisyon ID'sini doğru yöneten `BillingRoute`.

## Acceptance evidence

- PosTerminal Vitest testleri (`npm test`) başarıyla geçer.

## Handoff

- V1-RMD-059
