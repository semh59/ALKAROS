# V1-RMD-574 - PosTerminal lint kapısını yeşile döndürme

- Task ID: V1-RMD-574
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

`pnpm build` (Docker imajı derlemesinin parçası) lint kapısında 5 hatayla durur; hatalar giderilir ve imaj derlenebilir olur.

## Owned surface

- `plan/v1/remediation/V1-RMD-574-posterminal-lint-gate.md`
- `evidence/V1-RMD-574/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-user-journeys/** - yalnız boş tablo başlığı etiketleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-error-events/** - yalnız boş tablo başlığı etiketi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx - yalnız oturum referansının yazım yeri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız yeni sütun başlığı metni
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Boş `<th />` ve çubuk hücresine erişilebilir etiket; `Cashier.tsx` içinde oturum referansının render sırasında değil etki içinde güncellenmesi.
- `oxlint` hatasız, uyarı sayısı sınırın altında; mevcut testler yeşil.

## Out of scope

- Uyarıların (autoFocus, rol uyarıları vb.) giderilmesi.

## Dependencies

- V1-RMD-548
- V1-RMD-565

## Acceptance evidence

1. `oxlint -c .oxlintrc.json --max-warnings=20 src` çıkış kodu 0.
2. PosTerminal testlerinin tamamı ve `tsc --noEmit` yeşil.
3. Çıktılar `evidence/V1-RMD-574/` altındadır.

## Handoff

- None
