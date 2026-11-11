# V1-RMD-941 - PosTerminal hesap bölme ekranı hesap durumu hatasında bölme cümlesini göstersin

- Task ID: V1-RMD-941
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-936 incelemesinden: PosTerminal hesap bölme istemcisi (billingApi.ts) UNSUPPORTED_BILL_STATE için genel "Bu hesap durumunda değişiklik yapılamaz." der; bu istemci yalnız bölme tasarımı uçlarını çağırır ve sunucunun o işlem için cümlesi "Bu hesap durumunda bölme tasarımı değiştirilemez." Karar: istemci aynı cümleyi gösterir (kasiyer ekranıyla aynı).

## Owned surface

- `evidence/V1-RMD-941/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/billing/billingApi.ts
  - src/Clients/PosTerminal/src/features/billing/billingApi.test.ts
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-940

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
