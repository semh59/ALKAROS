# V1-RMD-854 incelemeler

1. qa-test-kalitesi: orta: waiter-load durum listesi tek durumla sınanıyordu -> İptal/Servis/Tamamlandı/Reddedildi dördü ayrı mutasyonla sınandı; orta/düşük: yarım kuruş girdisi gerçekçi değildi -> 2 adet, 1,01 TL, beklenen 0,51 (`ToEven` 0,50 verir); F0917 yargısının "dolaylı" olduğu RESULT'ta yazıldı; H18 için `SplitEngine.ApportionTax` -> `BillMath.Apportion` devri kayıtlı; M1 SplitDesign için de mutasyona eklendi.
2. qa-veritabani: kritik/orta yok; düşük: kısıt adı doğrulanmıyordu -> `ConstraintName` eklendi; plan yüzeyindeki `CashCloseSafetyHttpTests.cs` değiştirilmedi, bu RESULT'ta açıkça yazıldı; migration sırası (019, 020, 021, 032) ve down dosyası kontrol edildi.
Doğrulama turu: iki incelemenin tespitleri kodla yeniden kontrol edildi (`SplitEngine.cs:321` devir, fixture sıralaması, `RefundTerminalSessionHttpTests.cs:207-229`); çürütülen bulgu yok.
