# V1-RMD-510 - PosTerminal hesap ekranında iptal ve iade

- Task ID: V1-RMD-510
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Kasa sayfasındaki (`split-payment`) iptal ve iade akışının PosTerminal (React) hesap bölme ekranında da bulunması (`features/billing/BillSplitWorkspace.tsx`). Kapsam taslaktır; başlamadan önce Owned surface netleştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-510-posterminal-bill-cancel-and-refund-ui.md`
- `evidence/V1-RMD-510/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/refundApi.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/refundApi.test.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/RefundPanel.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/index.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/billing.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Hesap bölme ekranının altında "İptal ve iade" paneli: hesabın tahsilat satırları (`GET .../tenders/`), gerekçeli iptal (para yoksa), ödeme başına iade isteme, bu hesabın iade istekleri, onayla/reddet ve elle iade sonucunu fiş numarasıyla tamamlama. Düğmeler oturum yetkilerine (`bills.cancel`, `payments.refund.request`, `payments.refund.approve`) göre gösterilir; hata metinleri sabit Türkçe cümlelerdir, ham sunucu mesajı gösterilmez; Türkçe metinler `strings.ts` üzerinden.

## Out of scope

- Sunucu uçları (V1-RMD-506/508); kasa sayfası (V1-RMD-509).

## Dependencies

- V1-RMD-509

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-510/` altındadır.

## Handoff

- None
