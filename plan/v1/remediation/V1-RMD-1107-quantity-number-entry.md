# V1-RMD-1107 - Satırda adede dokununca sayı girişi

- Task ID: V1-RMD-1107
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: 10 adet için artı düğmesine 9 kez basılıyor. Sipariş satırındaki adet sayısına dokunulunca sayı girişi açılır; Enter veya alan terk edilince mevcut adet değiştirme çağrısı yeni adetle çalışır, Escape vazgeçer, geçersiz veya 0 adet yok sayılır (kaldırma ayrı düğmedir). Ürün ızgarasındaki meşgul kilidi bu görevde DEĞİŞMEZ: ardışık dokunuş kuyruğu revizyon çakışması riski taşır; RESULT.md'de risk olarak raporlanır.

## Owned surface

- `evidence/V1-RMD-1107/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/routes/Cashier.quantity-entry.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Ürün ızgarası kilidi; mutfaksız akış; sunucu sözleşmesi

## Dependencies

- V1-RMD-1106

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
