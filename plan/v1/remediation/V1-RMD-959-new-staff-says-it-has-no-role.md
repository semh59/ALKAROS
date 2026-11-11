# V1-RMD-959 - Yeni personelin rolü '—' görünmesin, rol atanmadığı söylensin

- Task ID: V1-RMD-959
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: personel hesabı açılınca listede Roller sütununda '—' çıkıyor, yönetici rolün nasıl atanacağını anlamıyor. Karar: sütun 'Rol atanmadı' der ve hesap açıldı mesajı rolün 'Rolleri düzenle' ile atanacağını söyler.

## Owned surface

- `evidence/V1-RMD-959/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.tsx
  - src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.test.tsx
  - src/Clients/PosTerminal/src/features/management-staff-roles/StaffSection.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-958

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
