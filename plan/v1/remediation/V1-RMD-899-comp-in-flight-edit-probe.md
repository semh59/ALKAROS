# V1-RMD-899 - İkram isteği sürerken tutarın tüm hesaba çevrilip onayın atlatılması şüphesi: önce davranışı çalıştıran test

- Task ID: V1-RMD-899
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: validation
- Surface state: Existing

## Goal

Kasiyer ekranı `submitComp`: tutar hesabın kalanına eşit ya da büyükse tek seferlik onay istenir. Şüphe: istek yoldayken tutar alanı kalanın tamamına çevrilirse, cevap geç gelince onay atlanmış olur. İndirim formunda (V1-RMD-893) bu açık kapatıldı; ikram formunda alanlar istek sürerken `disabled` render ediliyor. Önce davranışı çalıştıran test yazılır (istek sürerken her ikram alanı ve düğmesi kilitli, cevaptan sonra açılır, tek istek gider). Kilit yeterliyse şüphe çürütülür ve testler kanıt olarak kalır; değilse düzeltme plan değişikliğiyle eklenir.

## Owned surface

- `evidence/V1-RMD-899/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-comp-in-flight.test.js — yeni test
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı/yeşil davranış testi: istek sürerken ikram alanları kilitli
2. İki bağımsız qa incelemesi

## Out of scope

- Üretim kodu değişikliği (şüphe gerçek çıkarsa ayrı plan değişikliği)

## Dependencies

- V1-RMD-893

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
