# V1-RMD-526 - Kısmi iadenin fiş ve raporlarda görünmesi

- Task ID: V1-RMD-526
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

Kısmi iade sonrası bilgi fişi iade edilen kalemleri ve net tutarı gösterir; kanal ve ürün raporları iade edilen kalem/adedi düşer (bugün kanal raporu iadeyi netlemiyor).

## Owned surface

- `plan/v1/remediation/V1-RMD-526-item-refund-receipt-and-reports.md`
- `evidence/V1-RMD-526/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Receipt.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Fiş yanıtındaki `billItemId` ve `refundedQuantity` V1-RMD-525 ile gelir; bu görevde basılan bilgi fişi iade edilen adetleri ve net tutarı gösterir; kanal/ürün raporlarının netleme kapsamı bu görevde doğrulanıp kayda geçer; rapor değişikliği gerekiyorsa ek yüzey ayrı plan işleminde eklenir.

## Out of scope

- Mali fiş / yazarkasa.

## Dependencies

- V1-RMD-524

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-526/` altındadır.

## Handoff

- None
