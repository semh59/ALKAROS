# V1-RMD-056 - PosTerminal infinite loop and billing participants

- Task ID: V1-RMD-056
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`PosTerminal/src/App.tsx` içindeki `TableRoute` React effect bağımlılık döngüsünü (`loadFloorPlanForZone` / `load`) çözerek sonsuz render döngüsünü engellemek; `BillingRoute` katılımcı (participants) türetimini ve adisyon navigasyonunu sağlamlaştırmak.

## Owned surface

- `plan/v1/remediation/V1-RMD-056-posterminal-infinite-loop-and-billing-participants.md`
- PO:2026-08-31 kararıyla App.tsx yüzeyi V1-RMD-058'e devredildi.

## In scope

- `TableRoute` içinde `loadFloorPlanForZone` fonksiyonunu `zones` state bağımlılığından bağımsızlaştırarak `load` ve `useEffect` arasındaki sonsuz tetikleme döngüsünü gidermek.
- `BillingRoute` içinde masa ve sipariş katılımcı akışını doğrulamak.

## Out of scope

- Host backend dosyalarını değiştirmek.

## Dependencies

- V1-RMD-055

## Deliverables

- Döngüsüz, kararlı çalışan `TableRoute` ve entegre `BillingRoute`.

## Acceptance evidence

- PosTerminal testleri (`npm test` / Vitest) başarıyla geçer.

## Handoff

- V1-RMD-057
