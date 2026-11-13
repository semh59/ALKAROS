# V1-RMD-1037 RESULT

Karar (Semih 2026-11-13): onay bekleyen (status 'Requested') iade istegi olan hesap, kalan tutar odense bile Odendi olmaz; yonetici onaylayip/reddedene kadar acik kalir.
Mevcut kural: hesap kapanisi BillClosureService.TryCloseAsync -> projector Blockers (NotFullyAllocated, HasPendingPayment, HasUnknownPayment). Ayni yola yeni engel HasOpenRefundRequest eklendi (IPaymentRepository.HasOpenRefundRequestAsync + projector). Kapanis tek yoldan (3 cagri: tender, sweeper, cari) gectigi icin her yolda zorlanir; Paid'e gecis baska yerde yok (qa-backend dogruladi).
Onay/red sonrasi hesap mevcut kapanis supurucusu ile (1 dk) kapanir; tam iade hesabi iptal eder (mevcut).
Ekran: split-payment.js closedHeading, bekleyen istek varken "Tahsilat Tamam, Hesap Acik" + "Iade istegi yonetici onayi bekliyor; karar verilene kadar hesap kapanmaz." ve yesil onay yerine notr isaret.
Kanit: red.log (sunucu testi Closed bekledigi NotSatisfied yerine) ve red-static.log; green: Host Billing 362/362, PaymentClosure modulu 25/25 (test projesine 183 migration baglandi), CashSession 111/111, CustomerAccounts 38/38, StaticApps 548/548; mutations.txt M1-M5 killed; client-build-gate: PosTerminal degismedi, gerekmiyor (exit 0).
Onceden var olan hata (bu gorevden bagimsiz, HEAD'de de kirmizi): PaymentTender.Tests ACancelledBillNoLongerLeavesOrderReceivedOnTheDisplay (iptalden sonra musteri ekrani Idle olmuyor); yeni gorev gerekir.
Acik (urun karari / kapsam disi): AwaitingManualRefund (kart/havale iadesi onaylandi, fis girilmedi) durumunda engel yok; bu aralikta hesap Paid olabilir, iade tamamlaninca odeme dusup hesap eksik Paid kalir (qa-backend). Karar disinda birakildi.
Elle senaryo: M-03 hesabina Iade ve iptal > Iade iste; kalan tutari ode: ekran "Tahsilat Tamam, Hesap Acik" der, masa kapanmaz; yonetici reddedince ~1 dk icinde Odendi olur.
