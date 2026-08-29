# V1-RMD-041 - Bill split production navigation and flow

- Task ID: V1-RMD-041
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

`BillSplitWorkspace` bileşenini `PosTerminal` `App.tsx` navigasyon ve yönlendirme rotasına bağlamak; eşit, kalem ve koltuk bazlı adisyon bölme işlemlerini gerçek adisyonlar üzerinde çalıştırmak; sahte ödeme onay metinleri içermeden bölme planını kaydetmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-041-bill-split-production-navigation-and-flow.md`
- PO:2026-08-29 kararıyla src/Clients/PosTerminal/src/App.tsx yüzeyi V1-RMD-044'e devredildi; bu historical task closed kalır.
- PO:2026-08-29 kararıyla src/Clients/PosTerminal/src/features/billing yüzeyi V1-RMD-048'e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-041/**`

## Dependencies

- V1-RMD-040

## Acceptance evidence

- `BillSplitWorkspace` `App.tsx` üzerinden açılabilir ve gerçek adisyonla çalışır.
- Eşit, kalem ve tutar dağılımları doğrulanır; ödeme başarı iddiası içermez.

## Handoff

- V1-RMD-042
