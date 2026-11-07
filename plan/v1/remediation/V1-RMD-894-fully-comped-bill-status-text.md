# V1-RMD-894 - Tamamı ikram/indirimle kapanan hesapta ekran "Hesap Ödendi" demez

- Task ID: V1-RMD-894
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer ödeme ekranı `renderPaid`, kalan tutar sıfıra inen her hesapta "Hesap Ödendi / Bu hesabın tamamı tahsil edildi." yazıyor. Tamamı ikramla kapanan hesapta hiç tahsilat yoktur; bu metin yanlış bilgi verir. Hiç tahsilat yapılmamışsa (toplam alınan sıfır): düşüşlerin hepsi ikramsa "İkramla Kapandı" / "Bu hesabın tamamı ikram edildi; tahsilat yapılmadı."; hepsi indirimse "İndirimle Kapandı" / "Bu hesap indirimle kapandı; tahsilat yapılmadı."; karışıksa "İndirim ve İkramla Kapandı" / "Bu hesap indirim ve ikramla kapandı; tahsilat yapılmadı."; hiç düşüş yoksa "Hesap Kapandı". Tahsilat varsa başlık "Hesap Ödendi" kalır; alt metin düşüş yoksa mevcut "Bu hesabın tamamı tahsil edildi.", düşüş varsa "Bu hesap tahsilat ve ikramla / indirimle / indirim ve ikramla kapandı." olur. Türkçe metin `docs/UI_STYLE_GUIDE.md` kurallarına uyar.

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
