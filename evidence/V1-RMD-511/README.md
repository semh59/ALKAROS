# V1-RMD-511 kanıtı

Sonuç: ödemeleri tamamen iade edilmiş siparişler (iptal edilmiş ve iade edilmiş ödemeli hesabı olan, ödenmiş hesabı olmayan) gün sonu sipariş adedinden, garsonun vardiya özeti satış toplamından, bahşiş havuzu garson sayısından çıkar; iptal edilmiş hesabın bahşişi havuza girmez.

- Hâlâ açık hesabı olan sipariş (tek ödemesi iade edilmiş olsa bile) ve yeniden hesap açılıp ödenmiş sipariş sayılmaya devam eder.
- Ürün marjı raporu zaten ödenmiş hesap tabanlıdır; değişmedi. Kanal (QR/Online) raporu kapsam dışı.
- Stok tüketimi iade ile geri alınmaz (yapılmış yemek): ürün kararı olarak bu görevde dokunulmadı.

Testler: gerçek Host + Postgres (Host Reporting 11 test, Billing Host 49 test). Mutasyon: 6 mutant, hepsi KILLED.
