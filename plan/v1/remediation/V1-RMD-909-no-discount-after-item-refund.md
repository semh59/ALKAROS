# V1-RMD-909 - Kalem iadesi yapılmış hesaba sonradan hesap indirimi/ikramı uygulanamaz (fazla iade oluşmasın)

- Task ID: V1-RMD-909
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-898 açığı (Semih kararı: kapat): bir kalem iade edildikten SONRA hesaba indirim/ikram uygulanırsa, iade edilen kalemin gerçek payı düşer ve müşteriye fazla ödeme yapılmış olur; bu fark sonraki iadelerden mahsup ediliyor, mahsup edecek iade kalmadıysa geri alınamıyor (REFUND_EARLIER_OVERPAID). Karar: fazla iadenin oluşmasını engelle. Kalem iadesi (Requested, AwaitingManualRefund, Completed) bulunan bir hesaba hesap düzeyinde indirim ya da ikram uygulanmaz: 409 DISCOUNT_AFTER_REFUND, Türkçe ("Bu hesapta kalem iadesi yapılmış; iadeden sonra indirim ya da ikram uygulanamaz."). Kontrol hesap kilitleri altında, indirimin yazıldığı işlemde yapılır (hesap-ödeme kilidi, hesap satırı, tahsis kilidi sırası) ve kuyruktaki ön kontrolde de yapılır; böylece onay beklemeden reddedilir. Eski veriler için mahsup mantığı (V1-RMD-898) korunur.

## Owned surface

- `evidence/V1-RMD-909/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitStore.cs
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - tests/Host/Experience/Billing/RefundThenDiscountHttpTests.cs — yeni test
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: kalem iadesi sonrası hesap indirimi ve hesap ikramı 409 DISCOUNT_AFTER_REFUND; iade yoksa indirim uygulanır
2. Kontrol ve hata sözlüğü
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Eski verideki fazla iadelerin düzeltilmesi, iade sonrası bahşiş ve kalem ikramı (kalem payını değiştirmez)

## Dependencies

- V1-RMD-908

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
