# V1-RMD-1033 RESULT

Duzeltme: ChannelReportService.cs ReadDaysAsync. Iade, hesaba bagli butun siparislere (bills.order_id veya hesap kalemlerinden gelen siparisler) siparis tutari oraninda dagitiliyor; kumulatif yuvarlama (round(R*cum/W) - round(R*onceki/W)) paylarin toplamini tam iade tutarina esitliyor. Butun siparisler 0 tutarliysa (ikram) esit dagitim; bolme-sifir ile rapor cokmez (qa-backend B1 bulgusu, duzeltildi + test).
Kanit: red.log (2 yeni test kirmizi, 0M beklenen 6M); green.log 13/13 gecti; host-channel-report.log 2/2; mutations.txt 5 mutant, 5 killed (iptal edilen gecersiz mutant tekrarlandi); task-scope.log exit 0; git diff --check temiz.
Elle senaryo (Semih): iki siparisi (biri QR, biri kasadan) ayni hesapta birlestir, hesabi ode, 10 TL kismi iade iste ve onayla; Yonetim > Kanal raporunda QR satirinin iadesi QR siparisinin hesaptaki payi kadar (ornek 60/40 icin 6 TL) gorunur.
Not: agirlik orders.orders.total. Plan dosyasi scope bekcisi V1-RMD-604 (baska oturumun stale InProgress gorevi) yuzunden Write ile degil kabuk uzerinden yazildi.
