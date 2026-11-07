# V1-RMD-897 - Kalem iadesinde en çok 1 kuruşluk kayma şüphesi: önce davranışı çalıştıran test

- Task ID: V1-RMD-897
- Status: InProgress
- Assignee: Unassigned
- Work type: validation
- Surface state: Existing

## Goal

Kalem iadesinde (`PaymentRefunds.RequestAsync`) iade tutarı kalem payından kümülatif fiyatlanır, ödemenin kalanını aşarsa kalanla sınırlanır (`Math.Min`) ve kalem toplamı ödemeyi kalem başına en çok 0,01 aşabilir. Şüphe: bu tolerans ve sınırlama, hesabın tamamı parça parça ve çok ödemeli iade edilirken toplam iadeyi ödenenden bir kuruş saptırabilir. Önce davranışı çalıştıran kırmızı testler yazılır (çok kalemli, indirimli hesap; ödeme bölünmüş; tüm kalemler ayrı iade istekleriyle iade edilir; her iade tutarı ve toplamı kuruşu kuruşuna ödenene eşit olmalı). Kayma gerçekse ayrı bir kapsam genişletmesiyle düzeltilir (plan değişikliği); değilse testler kanıt olarak kalır ve RESULT.md'ye çürütme gerekçesi yazılır.

## Owned surface

- `evidence/V1-RMD-897/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/ItemRefundRoundingHttpTests.cs — yeni test
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı/yeşil test: indirimli çok kalemli hesapta, tek ödemeyle ve iki ödemeyle, kalemler ayrı isteklerle iade edilince iade toplamı ödenen toplama eşit
2. İki bağımsız qa incelemesi

## Out of scope

- Üretim kodu değişikliği (şüphe gerçek çıkarsa ayrı plan değişikliği)

## Dependencies

- None

## Acceptance evidence

1. Test çıktıları ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
