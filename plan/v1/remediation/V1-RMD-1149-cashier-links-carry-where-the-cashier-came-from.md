# V1-RMD-1149 - Kasa bağlantıları kasiyerin geldiği yeri taşır (Kasa ve Hesap Ödeme)

- Task ID: V1-RMD-1149
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 10, küçük akış düzeltmeleri): satış ekranının "Kasa" bağlantısı kasa açıkken geldiği sayfayı taşımıyor (kasa sayfasındaki geri bağlantısı hep ana ekrana gidiyor), ve Hesap Ödeme'nin ödendi ekranındaki geri bağlantısı hesap bölme ekranından gelen kasiyeri "Bekleyen hesaplara dön" diye başka yere gönderiyor.

1. `CashSessionLink` ve kasa bandındaki bağlantı, kasa açıkken de `returnTo` taşır (şimdi yalnız kasa kapalıyken taşıyor); kasa sayfası zaten yalnız yeni oturum açtıktan sonra `returnTo`'ya yönlendirir, açık oturumu açınca yönlendirmez, yalnız geri bağlantısını ayarlar.
2. "Tahsilata geç" bağlantısı (hesap bölme ekranı) Hesap Ödeme sayfasına `from=billing` ekler; sayfa bunu tanır ve ödendi/iptal ekranlarında "Hesap bölmeye dön" bağlantısını `/billing?billId=...` adresine verir. Tanınmayan `from` değeri eskisi gibi "Bekleyen hesaplara dön" olur.
3. Sunucu sözleşmesi değişmez.

## Owned surface

- `evidence/V1-RMD-1149/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-back-link-origin.test.js
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.tsx
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. PosTerminal ve istemci testleri

## Out of scope

- Satış ekranından tek dokunuşla satışsız çekmece açma (gerekçe zorunlu, bkz. V1-RMD-1150); kasa sayfasının iç akışı

## Dependencies

- V1-RMD-1148

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. PosTerminal ve tests/Clients/StaticApps testleri exit code 0

## Handoff

- None
