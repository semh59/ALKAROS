# V1-RMD-504 - Mutabakat vakası geçmişi ve not ekleme ekranı

- Task ID: V1-RMD-504
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Sunucuda mutabakat vakasına not ekleme ve vakanın işlem geçmişini okuma uçları hazırdır; arayüz yalnız vaka durumunu değiştirir ve notu durum değişikliğiyle birlikte gönderir. Yönetici vakaya durum değiştirmeden not ekleyemez ve vakada kimin ne zaman ne yaptığını göremez.

## Owned surface

- `plan/v1/remediation/V1-RMD-504-reconciliation-case-history-and-notes-ui.md`
- `evidence/V1-RMD-504/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-closing/ClosingSection.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-closing/ClosingSection.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-closing/closingApi.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-closing/models.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Vaka satırında "Geçmiş": işlem geçmişi (işlem türü Türkçe etiketle, zaman, not/gerekçe metni).
- `reconciliation.manage` yetkisiyle durum değiştirmeden "Not ekle"; boş not sunucuya gönderilmez.
- Hata kodları Türkçe metne çevrilir, ham sunucu metni gösterilmez.

## Out of scope

- Sunucu uçlarının değiştirilmesi; vaka oluşturma (vakalar tarama ve olaylarla açılır).

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-504/` altındadır.

## Handoff

- None
