# V1-RMD-906 - Kısmi ödemesi alınmış açık hesapta tahsil edileni aşağı çekecek kalem ikramı reddedilir

- Task ID: V1-RMD-906
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı (V1-RMD-905 açığı): açık bir hesaba kısmi ödeme (tahsis) alınabiliyor ve ödeme hesap durumunu değiştirmiyor; böyle bir hesapta kalem ikramı ödenecek tutarı tahsil edilenin altına indirebiliyor. İndirim yolu bunu zaten reddediyor (BillDiscountBelowCollectedException, hesap başına tahsis kilidi); ikram yolu reddetmiyor. Karar: ikram sonrası ödenecek tutar, tahsil edilen eksi iade edilen tutardan azsa ikram REDDEDİLİR; Türkçe, anlaşılır hata verir (kasiyer önce ödemeyi iade eder). Kontrol, siparişi ve hesap kalemini yazan işlemin içinde, indirim yolundaki aynı ödeme-tahsis kilidi altında yapılır; hata 409 COMP_BELOW_COLLECTED. İade edilen tutar payments.payment_reversals'tan düşülür, böylece ödeme iade edildikten sonra ikram yapılabilir.

## Owned surface

- `evidence/V1-RMD-906/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs
  - src/Host/Experience/Billing/BillMoneyGuard.cs
  - tests/Host/Experience/Orders/Comp/OrderCompOnPartlyPaidBillHttpTests.cs — yeni test
  - tests/Host/Experience/Orders/Comp/ALKAROS.Host.Experience.Orders.Comp.Tests.csproj — ödeme tabloları göçleri
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: kısmi ödemeli açık hesapta tahsili aşacak ikram 409 COMP_BELOW_COLLECTED; sipariş kalemi ve hesap kalemi değişmez
2. Ödemeler hesap başına bill-settlement kilidi ardından ödeme-tahsis kilidi altında kontrol edilir (tender ile aynı sıra); yolda bekleyen (Pending, Unknown, ReconciliationRequired) ödeme varsa ikram reddedilir (qa tur 1)
3. Tahsil edilen eksi iade edilen hesaplanır: iade sonrası aynı ikram uygulanır; tahsil edilene eşit kalan tutar ikrama izin verir
4. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- İndirim yolunun iadeyi düşmemesi (ayrı inceleme konusu), kalem ikramının geri alınması

## Dependencies

- V1-RMD-905

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
