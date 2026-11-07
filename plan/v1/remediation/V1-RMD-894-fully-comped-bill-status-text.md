# V1-RMD-894 - Tamamı ikram/indirimle kapanan hesapta ekran "Hesap Ödendi" demez

- Task ID: V1-RMD-894
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer ödeme ekranı `renderPaid`, kalan tutar sıfıra inen her hesapta "Hesap Ödendi / Bu hesabın tamamı tahsil edildi." yazıyor. Tamamı ikramla kapanan hesapta hiç tahsilat yoktur; bu metin yanlış bilgi verir. Hiç tahsilat yapılmamış (toplam alınan sıfır) ve düşüşleri yalnız ikramdan oluşan hesapta başlık "İkramla Kapandı", alt metin "Bu hesabın tamamı ikram edildi; tahsilat yapılmadı." olur; hiç tahsilat yapılmamış ama indirim içeren hesapta "İndirimle Kapandı" / "Bu hesap indirim ve ikramla kapandı; tahsilat yapılmadı." olur. Tahsilatı olan hesapta mevcut "Hesap Ödendi" metni değişmez. Türkçe metin `docs/UI_STYLE_GUIDE.md` kurallarına uyar.

## Owned surface

- `evidence/V1-RMD-894/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-comped-close.test.js — yeni test
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — kapanış metni
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Gerçek vitest testi: tamamı ikram, indirim+ikram, tahsilatlı ve normal ödenmiş hesap için başlık/alt metin
2. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Sunucu tarafı; yazdırılan fişin içeriği

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
