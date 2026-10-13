# V1-RMD-510 - PosTerminal hesap ekranında iptal ve iade

- Task ID: V1-RMD-510
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Kasa sayfasındaki (`split-payment`) iptal ve iade akışının PosTerminal (React) hesap bölme ekranında da bulunması (`features/billing/BillSplitWorkspace.tsx`). Kapsam taslaktır; başlamadan önce Owned surface netleştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-510-posterminal-bill-cancel-and-refund-ui.md`
- `evidence/V1-RMD-510/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İptal gerekçesi, iade isteme, bekleyen iade onayı ve elle iade sonucu girişi; Türkçe metinler `strings.ts` üzerinden.

## Out of scope

- Sunucu uçları (V1-RMD-506/508); kasa sayfası (V1-RMD-509).

## Dependencies

- V1-RMD-509

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-510/` altındadır.

## Handoff

- None
