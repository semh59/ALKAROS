# V1-RMD-521 - Kasiyer vardiya iade bütçesi (arayüz)

- Task ID: V1-RMD-521
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

V1-RMD-520 sunucu uçları için arayüz: müdür kasiyerin vardiyasına iade bütçesi verir; kasiyer kalan bütçesini görür ve bütçe içindeki iadenin hemen tamamlandığını ("bütçeden iade") veya onaya gittiğini ayrı mesajla görür.

## Owned surface

- `plan/v1/remediation/V1-RMD-521-cashier-shift-refund-allowance-ui.md`
- `evidence/V1-RMD-521/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kasa sayfası ve PosTerminal iade paneli: iade sonucu Completed ise "İade bütçenizden tamamlandı", AwaitingManualRefund ise fiş numarası ister, Requested ise "Müdür onayına gönderildi".
- Kalan bütçe gösterimi; bütçe okunamazsa gösterim gizlenir, iade akışı etkilenmez.
- Müdür tarafı için kasiyere bütçe verme alanı (`PUT /api/v1/terminals/{terminalId}/billing/refund-allowance`; kendine verilemez, kullanılandan az olamaz); kalan bütçe `GET` aynı adresten okunur; iade yanıtında `approvedFromAllowance` alanı bütçeden tamamlamayı gösterir; tüm metinler Türkçe.

## Out of scope

- Sunucu davranışı (V1-RMD-520), kısmi iade.

## Dependencies

- V1-RMD-520

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-521/` altındadır.

## Handoff

- None
