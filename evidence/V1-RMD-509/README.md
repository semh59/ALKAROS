# V1-RMD-509 kanıtı

Sonuç: kasa hesap sayfasında (`payments/split-payment`) gerekçeli hesap iptali, ödeme başına iade isteme, bu hesabın iade isteklerini listeleme, onaylama/reddetme ve elle iadeyi fiş numarasıyla tamamlama var.

## Davranış
- Ödemesi olmayan açık hesapta "Hesabı iptal et" (gerekçe zorunlu, sunucu da zorlar); başarılı olunca "Hesap İptal Edildi" ekranı. Parası olan hesapta iptal gösterilmez.
- Ödenmiş/parası alınmış hesapta onaylı ödeme başına "İade iste" (gerekçeli); açık isteği olan ödeme için gösterilmez.
- "İade istekleri": durum Türkçe (Onay bekliyor, Elle iade bekliyor, Tamamlandı, Reddedildi); onay bekleyende Onayla/Reddet, elle iade bekleyende fiş numarası + "İadeyi tamamla". Onay hesabı kapatırsa iptal ekranı gösterilir.
- Hata metni sunucunun Türkçe mesajıdır; ham HTTP kodu gösterilmez. Yetkisiz işlem sunucuda 403 ile reddedilir (sayfa yetkiyi önceden bilmez).

## Kanıt
- `test-runs.log`: StaticApps 37/37 (10 yeni kasa sayfası testi).
- `mutations.log`: 13 mutant, hepsi KILLED.

## Açık
- PosTerminal (React) hesap ekranında bu akış yok: V1-RMD-510.
- Tarayıcıda gerçek Host ile elle deneme yapılmadı; testler jsdom + sahte fetch üzerinde. Sunucu tarafı V1-RMD-506/508'de gerçek Host + Postgres ile doğrulandı.
