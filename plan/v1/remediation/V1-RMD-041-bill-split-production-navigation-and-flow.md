# V1-RMD-041 - Bill split production navigation and flow

- Task ID: V1-RMD-041
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`BillSplitWorkspace` bileşenini `PosTerminal` `App.tsx` navigasyon ve yönlendirme rotasına bağlamak; eşit, kalem ve koltuk bazlı adisyon bölme işlemlerini gerçek adisyonlar üzerinde çalıştırmak; sahte ödeme onay metinleri içermeden bölme planını kaydetmek.

## Owned surface

- `src/Clients/PosTerminal/src/features/billing/**`
- `src/Clients/PosTerminal/src/App.tsx`
- `evidence/V1-RMD-041/**`

## Dependencies

- V1-RMD-040

## Acceptance evidence

- `BillSplitWorkspace` `App.tsx` üzerinden açılabilir ve gerçek adisyonla çalışır.
- Eşit, kalem ve tutar dağılımları doğrulanır; ödeme başarı iddiası içermez.

## Handoff

- V1-RMD-042
