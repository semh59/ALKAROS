# V1-RMD-1046 - Yönetici, "Elle iade bekliyor" iadeyi POS ekranından sebep göstererek iptal eder

- Task ID: V1-RMD-1046
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1045 sunucuda onaylı ama tamamlanmamış iadenin yönetici tarafından zorunlu nedenle iptaline izin verdi; ret uç noktası bu durumu da kabul eder. POS terminal iade panelinde "Elle iade bekliyor" satırı için payments.refund.approve yetkisi olan kullanıcıya gerekçe alanı ve onaylı iki adımlı "Onaylı iadeyi iptal et" düğmesi eklenir; başarıda "Onaylı iade iptal edildi" bildirimi çıkar ve liste yenilenir. Yetkisiz kullanıcı (kasiyer) düğmeyi görmez. Semih kararı 2026-11-13.

## Owned surface

- `evidence/V1-RMD-1046/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1045

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
