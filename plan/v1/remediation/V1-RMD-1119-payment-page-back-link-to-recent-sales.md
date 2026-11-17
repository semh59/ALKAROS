# V1-RMD-1119 - Hesap Ödeme sayfası Son satışlardan açılınca geri bağlantısı Son satışlara döner

- Task ID: V1-RMD-1119
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 14: Son satışlar listesinden (V1-RMD-1118) iade için açılan Hesap Ödeme sayfasında geri bağlantısı "Bekleyen hesaplara dön" yazıp `/pending-checks` adresine gidiyor; kasiyer geldiği listeye değil başka ekrana düşüyor. Sayfa `from=recent-sales` parametresiyle açılırsa her geri bağlantısı "Son satışlara dön" yazar ve `/recent-sales` adresine gider; parametre yoksa veya başka değerdeyse mevcut bağlantılar aynen kalır. Parametre yalnız bu sabit değeri tanır, adres dışarıdan alınmaz. Sunucu sözleşmesi, iade kuralları ve ödeme akışı değişmez.

## Owned surface

- `evidence/V1-RMD-1119/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-cash-return-link.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. tests/Clients/StaticApps testleri

## Out of scope

- Sunucu sözleşmesi; iade ve ödeme kuralları; Son satışlar ekranı

## Dependencies

- V1-RMD-1118

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
