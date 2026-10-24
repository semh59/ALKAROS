# qa-performans-dayaniklilik - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 120 kalemin ~46'sı (12 tam okuma, 14 kısmi/grep, 20 göç indeks taraması). Okunmadı: saf hesap kodu (SplitEngine, Bill, BillItem, BillAdjustment, AdjustmentCalculator, BillMath), stiller. Test/benchmark koşulmadı; kilit davranışı yalnız kod okumasıdır.

| No | Önem | Kanıt | Sorun | Etki |
|---|---|---|---|---|
| P-01 | Orta | BillingSplitStore.cs:61,69,84-91,221-233 | İndirim/bahşişte hesap durumu ve PayableAmount FOR UPDATE'ten önce okunuyor; kilitten sonra durum yeniden kontrol edilmiyor. | Eşzamanlı iptal/kapanışta iptalli/Paid hesaba bahşiş eklenebilir; indirim yüzdesi bayat tutarla |
| P-02 | Orta | BillingSplitStore.cs:162,398-400,437,459; PostgresSplitDesignRepository.cs:332-335; Application.cs:598 | İndirim/bahşiş bills.row_version artırmıyor; bölme kaydı farkı InvalidOperationException ile reddediliyor -> 400; kayıtlı tasarım indirim sonrası bayat. | Eşzamanlı indirim+bölmede 409 yerine 400; tasarım tutarları uyuşmaz |
| P-03 | Orta | split-payment.js:124, cash-session.js:92, customer-accounts.js:86 | Üç Kasa sayfasında fetch zaman aşımı/AbortSignal yok (PosTerminal 8 sn). | Yarı açık bağlantıda busy sonsuz, ekran donar |
| P-04 | Orta | split-payment.js:612,437,491 | Idempotency anahtarı yalnız ağ hatasında korunuyor; 5xx/502/504'te yenisi üretiliyor. | Sunucu commit etmişse yeniden denemede mükerrer tahsilat |
| P-05 | Orta | DualScreenApplication.Payments.cs:85-120; BillClosureService.cs:42-52 | Hesap kapatma tahsilat sonrası tek seferlik best effort; süpürücü/yeniden deneme yok. | Ödenmiş hesap açık kalır, kalan 0, yeni tahsilat reddedilir |
| P-06 | Orta | PaymentRefundHttpTests, BillCancellationHttpTests (WhenAll yok) | İptal+tahsilat, çift onay, iade isteği+onay, bahşiş+kapanış, indirim+tahsilat eşzamanlılık testi yok. | P-01/P-02 yarışları yakalanmaz |
| P-07 | Düşük | Application.cs:574-603 | OperationCanceledException 500 + Error log (Orders'ta 499 yapıldı); 40P01/55P03 "DATABASE_UNAVAILABLE" 503, yeniden deneme yok. | Log gürültüsü, yanıltıcı mesaj |
| P-08 | Düşük | BillingSplitStore.cs:103-104,237-238 | Tekrar oynatma yalnız anahtara bakıyor (içerik karşılaştırılmıyor; null anahtar). | Yanlış içerikle aynı anahtar eski sonucu döner |
| P-09 | Düşük (Çıkarım) | BillingSplitStore.cs:84-93; PaymentRefunds.cs:110,214,216 | Kilitli işlem sürerken depolar havuzdan ikinci bağlantı açıyor. | Havuz tükenirse kilitlenme; tek restoranda gerçekçi değil |
| P-10 | Düşük | BillClosureService.cs:27; BillPaymentClosureProjector.cs:30 | Kapatma denemesinde hesap iki kez okunuyor, deneme başına ~5-6 bağlantı. | Küçük gereksiz yük |
| P-11 | Düşük | split-payment.js:188 | 5 sn yoklama önceki istek bitmeden başlıyor; login aşamasında zamanlayıcı durmuyor. | İstek birikimi |
| P-12 | Düşük | PaymentRefunds.cs:288-290,479-482 | cash_sessions sorgusu cashier_user_id+status, indeks yok. | Şimdilik ihmal edilebilir |
| P-13 | Düşük | tests/Performance/CriticalPaths/run.mjs:119-126; docs/performance | Bütçe yalnız bill.from-order ve payment.tender; indirim/bölme/iptal/iade/fiş yok. | Regresyon ölçülmez |
| P-14 | Orta | runs/PaymentTender.log | PaymentTenderHttpTests.cs:709 FAIL (Occupied beklendi, Available geldi); log yarım; Billing Host testleri için kayıt yok. | Kırmızı test = bulgu |

Temiz: indeksler (bills, bill_items, bill_allocations, bill_adjustments, payment_allocations, payments, payment_reversals, refund_requests, cash_transactions) mevcut, N+1 yok; liste tavanları (5000, LIMIT 200); kod okumasıyla kilit döngüsü bulunamadı (runtime doğrulanmadı); 40001 -> 409; iade/iptal tek işlemde, hub çağrısı yok; kapatma sonrası hub/sipariş tamamlama ayrı try/catch; nakit tahsilat replay kapatmayı yeniden deniyor; iade çift tık tek açık istek kuralıyla korunuyor; TableEventBillConsumer idempotent; bellek sızıntısı yok.

Doğrulanamayanlar: runtime kilit davranışı; Npgsql havuz tükenme istisnası; Host.Experience.Billing.Tests sonucu; bill.js/customer-accounts.js iç akışı. Semih senaryosu: indirimli hesapta ürün bazlı bölme reddediliyor (SplitItemsOnAdjustedBillException, BillingSplitStore.cs:438-439); indirim bölmeden sonra uygulanırsa tasarım bayat (P-02).
