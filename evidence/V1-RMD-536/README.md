# V1-RMD-536 kanıtı

Sapma motoru (modül düzeyinde, Host'a bağlı değil): aynı hafta günü ve saatin son 8 haftalık tabanına göre 5 dakikalık pencerede Z-skor. Gürültü korumaları: en az 4 hafta geçmiş, en az 5 mutlak olay, sapma tabanı 1 (sıfıra yakın seride tek olay alarm vermez), eşik Z ≥ 3. Yükseltme bir kademe (Warning→Error→Critical). Dedektör nabız sözleşmesi: kayıtlı dedektör beklenen aralığın 2 katından uzun sessizse `monitoring.detector_degraded` olayı.

## Kabul koşulları
1. Düşük tabanda tek hata yükseltme üretmez (SingleEventOnANearZeroSeriesNeverEscalates; ZeroSpreadBaselineUsesTheDeviationFloor).
2. Taban 10 kat aşılınca yükseltme olur (TenTimesTheBaselineEscalates; gerçek veritabanı geçmişiyle BaselineReaderFindsSameHourHistoryAndTheCurrentBurst). "5 dakika içinde" pencere uzunluğu 5 dk'dır; çalıştırıcı iş bağlanınca gecikme ölçülür.
3. Susan dedektör 2 aralıkta `detector_degraded` olur (SilentDetectorBecomesADegradedEventAfterTwoIntervals).
4. Test 10/10 geçti; mutasyon 7/7 öldürüldü (bir mutant ilk turda hayatta kaldı, test eklenerek öldürüldü).

## Doğrulanamadı / sınır
- Motor yalnız hesap yapar; periyodik Host işi ve bulguyu yazıcıya/ekrana bağlamak ayrı iştir. "Açılışta kendi kendine test" maddesi bu parçada yok.
- Taban saatlik dilimlerden hesaplanır; haftanın günü/saati yoğun restoranlarda yorumlama gerektirir.
