# V1-RMD-1043 RESULT

Karar (Semih 2026-11-13): kart/havale iadesi onaylanip fis girilmeden hesap Odendi olmasin; fis girilene kadar acik kalsin.
Duzeltme: 1037'nin ortak engeli (HasOpenRefundRequest, PostgresPaymentRepository.HasOpenRefundRequestAsync) 'Requested' yaninda 'AwaitingManualRefund' durumunu da sayar; ayni sorgu, ayni kapanis yolu (tek Paid atamasi BillClosureService, qa-backend dogruladi). Fis girilince (Completed) ya da reddedilince engel kalkar, mevcut supurucu hesabi kapatir.
Ekran: split-payment.js closedHeading, onaylanmis iade icin "Tahsilat Tamam, Hesap Acik" + Iade bolumunde "Iadeyi tamamla" (kart, fis no) / "Havale gonderildi" (havale) yonlendirmesi; onay bekleyen metin degismedi.
Kanit: red.log (Closed beklenen NotSatisfied yerine) + red-static.log; green: Host Billing 363/363, PaymentClosure 25/25, StaticApps 550/550; mutations.txt M1-M6 killed; src/Clients/PosTerminal degismedi (client-build-gate gerekmez).
Acik (urun karari, yapilmadi): AwaitingManualRefund'dan cikis yalniz fis/havale tamamlama; para fiilen iade edilmediyse "onayli iadeyi iptal et" yolu yok (kasiyer sahte fis girmeye zorlanabilir, gun sonu uyarisi gecilebilir). Fis sonrasi kapanis supürücüyle gecikmeli (~1 dk).
Elle senaryo: kartla odenmis hesapta Iade ve iptal > Iade iste; yonetici onaylasin: ekran "Tahsilat Tamam, Hesap Acik"; fis numarasini yazip Iadeyi tamamla: hesap acik kalmaz/yeniden tahsil ister ya da iptal olur.
