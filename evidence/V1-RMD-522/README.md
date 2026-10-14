# V1-RMD-522 kanıtı

Değişiklik: `BillPaymentClosureCalculator` kısmen iade edilmiş (PartiallyRefunded) ödemeyi ve tahsisatını Approved gibi sayar; tam iade edilmiş (Refunded) ödeme sayılmaz.

Doğrulananlar:
- Birim testleri: kısmen iadeli ödeme hesabı ödenmiş tutar; tam iadeli ödeme sayılmaz.
- Tender özeti (`DualScreenApplication.Payments.cs:147`) kalan tutarı tüm tahsisatların toplamından hesaplar, ödeme durumuna bakmaz; bu yüzden kısmi iade kalan tutarı değiştirmez.
- Kasa defteri yönü işlem türüne göre belirlenir (Refund = Out); kısmi tutar aynı kayıtla yazılır.

Doğrulanmayanlar: kasa oturumu kapanışında kısmi iadenin beklenen nakite etkisi çalıştırılarak denenmedi (V1-RMD-524 testinde kanıtlanacak).
