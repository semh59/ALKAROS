# V1-RMD-897 - Kalem iadesinde en çok 1 kuruşluk kayma şüphesi: önce davranışı çalıştıran test

- Task ID: V1-RMD-897
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kalem iadesinde (`PaymentRefunds.RequestAsync`) iade tutarı kalem payından kümülatif fiyatlanır, ödemenin kalanını aşarsa kalanla sınırlanır (`Math.Min`) ve kalem toplamı ödemeyi kalem başına en çok 0,01 aşabilir. Şüphe: bu tolerans ve sınırlama, hesabın tamamı parça parça ve çok ödemeli iade edilirken toplam iadeyi ödenenden bir kuruş saptırabilir. Önce davranışı çalıştıran kırmızı testler yazılır (çok kalemli, indirimli hesap; ödeme bölünmüş; tüm kalemler ayrı iade istekleriyle iade edilir; her iade tutarı ve toplamı kuruşu kuruşuna ödenene eşit olmalı). Kayma gerçekse düzeltilir; değilse testler kanıt olarak kalır ve RESULT.md'ye çürütme gerekçesi yazılır. Kayma doğrulandı: ödemeleri 95,99 ve 144,00 olan indirimli hesapta kalemler tek tek iade edilince toplam 239,98 çıkıyor (kırmızı test); ilk ödemeye sığmayan kuruş `Math.Min` ile kaybolup diğer ödemede kalıyor. Düzeltme: kalem iadesi, önceki kalem iadelerinde verilmeyen kuruşu (kalemlerin kümülatif payı eksi kalem iadeleriyle verilen para, en az 0) bir sonraki kalem iadesine ekler; ödeme kalanını aşamaz.

## Owned surface

- `evidence/V1-RMD-897/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/ItemRefundRoundingHttpTests.cs — yeni test
  - src/Host/Experience/Billing/PaymentRefunds.cs
  - src/Modules/Payments/PaymentAggregate/PostgresRefundRequestRepository.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı/yeşil test: indirimli çok kalemli hesapta, tek ödemeyle ve iki ödemeyle, kalemler ayrı isteklerle iade edilince iade toplamı ödenen toplama eşit
2. Düzeltme: önceki kalem iadelerinde eksik kalan kuruş sonraki kalem iadesine eklenir
3. İki bağımsız qa incelemesi

## Out of scope

- Kalem olmayan (tutar) iade ve bir kalemi ödemeler arasında bölme davranışı değişmez

## Dependencies

- None

## Acceptance evidence

1. Test çıktıları ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
