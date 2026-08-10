# V20-UAT-002 - Accept finance and inventory workflows

- Task ID: V20-UAT-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.45-I.54

## Goal

Billing, Payment, refund, CashSession, CustomerAccount, Invoice, purchasing, stock ve reporting workflow'ları için named
user acceptance toplamak.

## Owned surface

- `release/evidence/uat/finance-inventory/**`
- Bu görev ürün kodunu veya acceptance sonucunu değiştiremez.

## In scope

- Payment/refund, CashSession close, CustomerAccount posting/payment, Invoice lifecycle, receipt/adjustment,
  ProductionBatch consumption, Waste ve report reconciliation.

## Out of scope

- Service UI, legal approval, defect fix ve production kullanımı.

## Dependencies

- V20-REL-001
- V15-RPT-001
- V20-INT-001
- V20-INT-002
- V20-INT-004

## Deliverables

- Çalıştırılmış named scenario script'leri, participant sign-off kayıtları ve financial/stock control total değerleri.

## Acceptance evidence

- Her zorunlu scenario script'i geçer ve control total değerleri reconcile edilir; failed veya unexplained divergence
  acceptance'ı engeller.
- `V20-INT-001`, `V20-INT-002` veya `V20-INT-004` kanıtlı `NotApplicable` ise ilgili provider certification senaryoları
  UAT kapsamına dahil edilmez; kalan zorunlu scenario script'leri yine geçer ve control total değerleri reconcile
  edilir.
- `V15-RPT-001` kanıtlı `NotApplicable` ise birleşik raporlama senaryoları UAT'e dahil edilmez; kalan zorunlu scenario
  script'leri yine geçer ve control total değerleri reconcile edilir.
- `V20-REL-001` kanıtlı `NotApplicable` ise release adayı paketlemesi beklenmez; kalan zorunlu scenario script'leri
  yine geçer ve control total değerleri reconcile edilir.

## Handoff

- V20-UAT-003
