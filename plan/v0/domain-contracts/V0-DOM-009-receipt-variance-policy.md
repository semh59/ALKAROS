# V0-DOM-009 - Define receipt variance policy

- Task ID: V0-DOM-009
- Status: Done
- Assignee: codex-v0-dom-009
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:III.15
- CORR:C11

## Goal

Satın alma siparişi ile teslim alınan miktar arasındaki eksik, fazla ve reddedilen miktarların tek bağlayıcı
politikasını belirlemek.

## Owned surface

- `docs/domain/receipt-variance-policy.md`
- Bu görev başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Tolerance, approval, reason, supplier effect, inventory posting ve audit zamanlaması.

## Out of scope

- Goods receipt production code, supplier accounting ve UI.

## Dependencies

- V0-CMP-002

## Deliverables

- Tek decision record: kaynaklar, erişim tarihleri, onaylayan, seçilen sonuç, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Eksik, tam, fazla ve reddedilen teslim için pozitif/negatif örnek matrisi.

## Acceptance evidence

- Her variance durumu tek sonuç, yetki ve stock/supplier etkisi üretir; onaysız over-receipt davranışı yoktur.

## Handoff

- V11-PUR-001
