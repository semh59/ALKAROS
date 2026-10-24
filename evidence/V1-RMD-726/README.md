# V1-RMD-726 - Sıfır tutarlı hesap kapanışı ve yüzde yüz indirim (603 B001)

**Sonuç:** Ödenecek tutarı sıfır olan hesap artık kapanıyor. Kasada tüm hesabı kapatan indirim (örn. %100) bir kez onay soruyor; onaylanınca sunucu hesabı Paid yapıyor ve siparişi tamamlıyor, ekranın "Ödendi" durumu sunucuyla uyumlu.

## Değişiklik
- `BillPaymentClosureCalculator.cs`: `adjustedPayable > 0 &&` şartı kalktı; ödenecek 0 olan hesap tahsilatsız tatmin sayılır.
- `BillingSplitApplication.cs`: indirim uygulanınca ödenecek 0 ise `TryCloseBillAsync` (hesap kapanışı, sipariş tamamlama, müşteri ekranı bildirimi); bağımsız kompozisyon için kapanış zinciri `AddBillingSplitExperience` içinde kayıtlı.
- `split-payment.js`: indirim kalan tutarın tamamını alıyorsa "Evet, indirimi uygula" ikinci adımı; değer/tür değişince onay sıfırlanır.

## Kanıt (logs/)
- PaymentClosure modül testi 18/18 (yeni: tam indirimli hesap, ödeme yok). Host Billing 72/72 (yeni: `ADiscountThatTakesThePayableToZeroClosesTheBill`, hesap Paid olur). StaticApps 122/122 (yeni 2: büyük indirimde onay istenir ve onaydan önce istek gitmez; küçük indirim doğrudan gider). Mimari: ModuleBoundaries 17/17, ApiConventions 5/5.
- PaymentTender 52/53: kırmızı olan `ACheckThatWasAlreadySentToTheTillCompletesButItsTableIsNeverTouched`, 603 B003 (test tohumu, V1-RMD-728); bu görevle ilgisiz.
- Mutasyon (`mutations.log`): 4/4 KILLED, kaynak geri yüklendi (restored=true).

## Açık risk
İndirim yetkisi olmayan rolde istek yönetici onayına gider (202); onaylanan indirimin sonradan uygulanması bu görevde incelenmedi ve test edilmedi. Doğrulanmadı; 729 kapsamında (B010, B011) bakılacak.
