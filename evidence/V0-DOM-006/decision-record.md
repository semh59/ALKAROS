# V0-DOM-006 Decision Record — approved

- Task: V0-DOM-006
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:II.2.5, PDF:II.3.3, PDF:II.5.2, PDF:III.7
- Access date: 2026-08-02
- Result: Approved
- Artifact: `docs/domain/void-complimentary-discount-policy.md`

## Decision summary

- Void: yalnız hazırlanmamış order item (`kitchen_state NotSent`); zorunlu
  reason kataloğu; her void `Manager` yetkisi gerektirir; `order_status_history`
  audit kaydı (PDF I.28.1, III.6.4).
- Complimentary: ürün teslim edilir bedel 0; `Manager` yetkisi + zorunlu
  reason + audit; `line_type Complimentary` sıfır vergi bazı (PDF I.28.1,
  III.7.2).
- Discount: line-seviyesi `discount_amount`; `Discount` satırı yalnız fiyat
  farkını taşır; dağıtım V0-CMP-002 per-line round-half-up kuralıyla
  orantılı (PDF III.6.2, III.7.2).
- Her sıfır/negatif fiyat etkisi için tek yetki + tek audit kuralı; sessiz
  etki yok.
- Waste/Refund bu görevin kapsamında değil (V0-DOM-003 ve stok alanı).

## Verification

- PDF satırları: I.28.1 (void/refund/waste/comp tanımları, 515-521), II.3.3
  (re-open/void policy, 1050-1054), III.6.2 (order_items status + discount,
  1624-1631), III.7.2 (bill_items line_type, 1684-1690).
