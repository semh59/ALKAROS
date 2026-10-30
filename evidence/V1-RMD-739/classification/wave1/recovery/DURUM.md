# Dalga 2 — kurtarılan ara sonuçlar

Aktif görev V1-RMD-739. Uygulama kodu ve testleri değiştirilmedi/çalıştırılmadı.

## Güncel karar önceliği

- Aşağıdaki eski ara sayımlar tarihçedir; güncel kabul kapsamı değildir. Ajan tamamlanması veya JSON sayısı inceleme kabulü sayılmaz.
- Kabul edilmiş sınıflama kararlarının giriş noktası: `../adjudication/INDEX.json`. Listelenen kaynaklarda bu kararlar eski recovery/worker taslaklarından önceliklidir; taslaklar silinmez.
- Paket 01: F0388 ve F0908 için `not-defect` elemeleri reddedildi; ikisi açık belge kusurudur. Özgün kayıt, güncel kaynak ve bağımsız denetim karşılaştırıldı; karar `../adjudication/packet01/ledger.json` içine işlendi.
- Bu paket yalnız iki kaydın statik sınıflamasını kabul eder. Belge düzeltmesi, uygulama/test doğrulaması veya kusur kapanışı değildir.
- Diğer kayıtlar bu paketle onaylanmaz; 27 durum çatışmasının tamamı ve 14 kimlik sorunu çözülmüş değildir.

## Önceki ara raporlar — kabul edilmiş sonuç sayılmaz

- İlk 10 büyük paketten 7'si dosya üretmeden bitti; teslimat sayılmaz.
- 03, 04, 09 paketlerinin gerçek ara dosyaları içe alındı: 70 kaynak için 69 değerlendirme taslağı. Aynı paketlerde 79 kaynak açıkça bekliyor.
- 69 değerlendirme: 55 open, 2 decision-blocked, 2 verification-needed, 8 not-defect, 2 historical-fixed. Bunlar henüz bağımsız içerik kontrolü tamamlanmış sonuçlar değildir; bugünkü toplam tekil kusur sayısına eklenmez.
- Kimlik kümesi, parent bağlantıları, pending ayrımı, dosya/satır varlığı kontrolleri geçti. Kanıt: PARTIAL_IMPORT_VALIDATION.json. Bu, statik iddiaların doğruluğu için bağımsız inceleme değildir.
- Parent tarafından ayrıca iki belge kusuru kaydedildi: F0908 ve F0388. 70 taslak kaynakla ayrık; bağımsız denetim bekliyor.
- 10 eleme/tarihsel düzeltme iddiası, iki parent kaydı ve üç kritik mekanizma ayrı bağımsız incelemeye verildi. Her kayıt sonrası checkpoint zorunlu.
- Eksik yedi büyük paketin ilk beşer kaynaklık kurtarması tamamlandı ve dosyaları doğrulandı; bu, bağımsız içerik kabulü değildir.
- Son kurtarma: 35 ayrı kaynak dosyası içe alındı; 44 değerlendirme (34 open, 5 verification-needed, 2 decision-blocked, 2 not-defect, 1 historical-fixed). RECOVERY_35_VALIDATION.json kimlik/HEAD/referans kontrolleri PASS; bağımsız içerik denetimi bekliyor.
- Sonraki 35 kaynak yedi küçük parçada incelemeye verildi; her kaynak sonrası dosya zorunlu.
- 30 kayıt ve 32 değerlendirme (20 open, 1 historical-fixed, 2 decision-blocked, 8 verification-needed, 1 not-defect) NEXT_* olarak kaydedildi ve doğrulandı. 1 kayıt (batch05) hala çalışıyor.

Dosyalar: BATCH_03_PARTIAL.json, BATCH_04_PARTIAL.json, BATCH_09_PARTIAL.json, F0908.json, F0388.json.
