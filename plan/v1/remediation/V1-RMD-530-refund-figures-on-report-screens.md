# V1-RMD-530 - Kanal ve ürün raporu ekranlarında iade tutarları

- Task ID: V1-RMD-530
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

Yönetim kanal raporu ekranı her satırda ve toplamda iade edilen tutarı gösterir; ürün kârlılığı ekranı kalemlere dağıtılmamış iade toplamını ayrı satırda gösterir.

## Owned surface

- `plan/v1/remediation/V1-RMD-530-refund-figures-on-report-screens.md`
- `evidence/V1-RMD-530/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-channel-report/models.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-channel-report/ChannelReportSection.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-channel-report/ChannelReportSection.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-product-margin-report/models.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-product-margin-report/ProductMarginSection.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-product-margin-report/ProductMarginSection.test.tsx
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kanal raporunda "İade edilen" sütunu ve toplamı (`refundedValue`).
- Ürün kârlılığında `unallocatedRefunds` sıfırdan büyükse açıklamalı ayrı satır.

## Out of scope

- Sunucu tarafı; yeni uç nokta.

## Dependencies

- V1-RMD-527
- V1-RMD-529

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-530/` altındadır.

## Handoff

- None
