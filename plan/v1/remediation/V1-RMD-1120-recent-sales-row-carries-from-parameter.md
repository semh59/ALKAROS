# V1-RMD-1120 - Son satışlar satırı ödeme sayfasını from=recent-sales ile açar

- Task ID: V1-RMD-1120
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 14: Hesap Ödeme sayfası V1-RMD-1119 ile `from=recent-sales` parametresini tanır ama Son satışlar satırı (V1-RMD-1118) bu parametreyi göndermiyor. Satıra dokununca açılan adres `billId`, `open=refund` ve `from=recent-sales` taşır; böylece iade sayfasındaki geri bağlantısı "Son satışlara dön" yazar. Sunucu sözleşmesi, iade kuralları ve ekranın diğer davranışı değişmez.

## Owned surface

- `evidence/V1-RMD-1120/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/recent-sales/recentSalesApi.ts
  - src/Clients/PosTerminal/src/features/recent-sales/RecentSalesWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci typecheck, testler ve üretim derlemesi (client-build-gate)

## Out of scope

- Hesap Ödeme sayfası; sunucu sözleşmesi; iade kuralları

## Dependencies

- V1-RMD-1119

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
