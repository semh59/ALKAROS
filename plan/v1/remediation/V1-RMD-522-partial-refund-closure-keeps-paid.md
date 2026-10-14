# V1-RMD-522 - Kısmi iadeli ödeme hesabı açık göstermesin

- Task ID: V1-RMD-522
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

Doğrulanan risk: `BillPaymentClosureCalculator` yalnız Approved ödemeyi sayar; kısmen iade edilen ödeme PartiallyRefunded olunca ödenen/dağıtılan tutar düşer ve ödenmiş hesap tekrar tahsilat bekliyormuş gibi görünebilir. PO kararı: kısmi iadeden sonra hesap Ödendi kalır. Bu görev kısmen iade edilmiş ödemeyi kapanış hesabında brüt tutarıyla saymaya devam ettirir; tam iade hesabı zaten iptal eder.

## Owned surface

- `plan/v1/remediation/V1-RMD-522-partial-refund-closure-keeps-paid.md`
- `evidence/V1-RMD-522/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/BillPaymentClosureCalculator.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/PaymentClosure/BillPaymentClosureCalculatorTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- PartiallyRefunded ödeme, kapanış projeksiyonunda ödenen ve dağıtılan toplama Approved gibi girer; Refunded girmez.
- Sınırlı doğrulama: tender özeti (kalan tutar) kısmi iadeden sonra 0 kalır; kasa oturumu kapanışında kısmi iade etkisi doğrulanır ve bulgu kayda geçer.

## Out of scope

- Kısmi iade isteği ve arayüzü (V1-RMD-524, V1-RMD-525).

## Dependencies

- V1-RMD-508

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-522/` altındadır.

## Handoff

- None
