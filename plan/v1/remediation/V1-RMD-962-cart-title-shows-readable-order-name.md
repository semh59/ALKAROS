# V1-RMD-962 - Sepet başlığı teknik sipariş kodu göstermesin

- Task ID: V1-RMD-962
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: aktif sipariş başlığında POS-20261111-143012-A1B2C3D4 gibi teknik sipariş kodu görünüyor. Karar: sunucunun üretim saatini taşıyan POS kodu başlıkta 'Sipariş 14:30' olarak gösterilir; tanınmayan biçimde 'Sipariş ' + kod gösterilir. Kod verisi ve API değişmez.

## Owned surface

- `evidence/V1-RMD-962/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.first-touch.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-961

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
