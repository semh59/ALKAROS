# V1-RMD-1125 - Son satışlar: erişilemeyen 401 cümlesi kalkar, kasa kapalıyken tek mesaj

- Task ID: V1-RMD-1125
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 16: (1) V1-RMD-1124'te eklenen "Oturum süresi doldu, yeniden giriş yapın." cümlesi ekranda hiç görünmüyor, çünkü 401'de kabuk oturumu kapatıp giriş ekranına "Oturumunuz sonlandırıldı…" koyuyor; ölü cümle, ona özel kod ve test kaldırılır, oturum sonlandırma davranışı değişmez. (2) Kasa kapalıyken Son satışlar sayfasında üstteki "Kasa oturumu kapalı…" bandı ile sayfadaki "Son satışları görmek için önce kasayı açın" aynı şeyi söylüyor; band görünüyorsa sayfa mesajı tekrarlanmaz, band yoksa sayfa mesajı kalır.

## Owned surface

- `evidence/V1-RMD-1125/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/recent-sales/recentSalesApi.ts
  - src/Clients/PosTerminal/src/features/recent-sales/RecentSalesWorkspace.tsx
  - src/Clients/PosTerminal/src/features/recent-sales/RecentSalesWorkspace.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci typecheck, testler ve üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu sözleşmesi; yukarıda sayılmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1124

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. client-build-gate çıktısı exit code 0

## Handoff

- None
