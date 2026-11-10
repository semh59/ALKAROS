# V1-RMD-899 sonucu

Şüphe ÇÜRÜTÜLDÜ. İkram isteği yoldayken tutar alanını kalanın tamamına çevirip tek seferlik onayı atlatmak mümkün değil: `renderCompForm` istek sürerken (`state.compBusy`) gerekçe, tutar, not, "Kalanın tamamı" ve gönder düğmesini `disabled` çiziyor; cevaptan sonra açıyor. İndirim formundaki açık (V1-RMD-893) ise alanların kilitlenmediği için gerçekti ve orada kapatıldı.

## Kanıt
- Test: tests/Clients/StaticApps/split-payment-comp-in-flight.test.js (istek 80 ms gecikmeli: beş denetim de kilitli, cevaptan sonra açık, tek istek gider). green-1.log 1/1.
- Mutantlar (mutations.txt): her denetimin kilidini ayrı ayrı kaldıran 7 mutant öldü (5 kilit + compBusy hiç kurulmuyor + hiç temizlenmiyor) (kod her seferinde geri yüklendi, "restored-identical").
- Üretim kodu değişmedi.
