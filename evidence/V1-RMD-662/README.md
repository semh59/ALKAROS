# V1-RMD-662 — QR bağlayıcı anahtarı, açılış doğrulaması, ölü mektup izleyicisi

Kapatılan bulgular: 596-B002, B012, B016.

## Sonuç
- Docker kurulumunda `connector` servisi artık `api` ile aynı `ALKAROS_SECRET_ENVELOPE_MASTER_KEY` değerini alır (B002).
- Bağlayıcı bu anahtar yoksa açılışta "ALKAROS_SECRET_ENVELOPE_MASTER_KEY is required." diyerek çıkış kodu 2 ile durur; ilk tünel denemesinde şifre çözme hatası olarak çıkmaz (B012).
- Ölü mektup izleyicisi, açılışta zaten var olan ölü kayıtları ilk kontrolde bildirir (B016).
- Bu görevde ayrıca Deployment sözleşme testlerinde önceden kırık duran 3 test düzeltildi (Dockerfile'da 6 aşama, 4 servis parola/entrypoint kullanıyor; sayılar bayattı).

## Kanıt
- test-runs.log: SecurityDetectors 5/5, Deployment 9/9, bağlayıcı ve test projesi temiz derleme (0 uyarı 0 hata).
- mutations.log: 3 mutant, 3 öldü (ilk koşuda "environment bloğu bozuldu" mutantı hayatta kaldı, test bloğu ayrıştıracak şekilde güçlendirildi, yeniden koşuda öldü).

## Doğrulanamadı
- Bağlayıcının anahtarsız açılışta durması (Program.cs) için otomatik test yok; kod dört satırlık bir erken çıkış. Docker'da gerçek çalıştırma yapılmadı (bu makinede Docker yok).
- API Host'un anahtarsız açılması bu görevde değiştirilmedi: compose `:?` ile zorunlu kılıyor; yerel geliştirmede anahtarsız çalışır.

## Sonraki göreve ayrılanlar
B007 ve B021 (belge/sözleşme), B010 (mutfak olayı atomikliği), hassas veri sertleştirme (B015, B019, B020, B022, B025, B026), test ve mimari kapsam (B011, B013), belge/lisans (B023, B024).
