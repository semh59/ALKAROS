# V1-RMD-471 - Satıcı (işletme) bilgileri: e-Fatura ayarları ekranındaki kart

Online sipariş faturalarında görünen işletme bilgisi (ticari ünvan, VKN/TCKN, vergi dairesi, adres, e-posta) yönetici tarafından girilir ve kaydedilir.

## Değişiklikler

- `src/Host/Experience/InvoiceSettings/`: `GET` ve `PUT /api/v1/terminals/{terminalId}/invoice-settings/seller-profile`. Kasiyer oturumu + `integrations.manage` izni;
  eksik veya hatalı alanlarda 400 ve Türkçe alan adlarıyla mesaj. `DualScreenApplication.cs` kaydı.
- `src/Clients/PosTerminal/src/features/invoice-settings/`: API istemcisi ve `SellerProfileCard`; `routes/QnbCredentialSettings.tsx` içinde QNB kartının altına bağlandı
  (aynı yönetici oturumu ve yetki kapısı). Metinler `strings.ts` > `invoiceSettingsText`.
- Kullanılmayan-servis izin listesinden iki satıcı-deposu kaydı çıktı (artık Host çağırıyor).

## Kanıt

- `green-host.log`: QnbCredentialSettings test projesi 17/17 (3 yeni HTTP testi: oturum yok 401, izinsiz 403, kaydet-oku-değiştir, eksik alan reddi ve hiçbir şey saklanmaması).
- `mutation-host.log`: izin kontrolü ve doğrulama kaldırılınca 2 test kırmızı, mutasyonsuz akış geçiyor; dosya geri alındı, hash özdeş.
- `vitest.log` 57 dosya / 424 test yeşil; `typecheck.log`, `lint.log`, `build.log` exit 0.
- `mutation-run.log` ve `mutation-run-email.log`: boş e-postanın `null` gönderilmesi, sunucunun Türkçe mesajının gösterilmesi ve kartın ekrana bağlanması bozulunca testler kırmızı.
- `trial.live.log` + `trial.png`: gerçek Host ve Postgres, tarayıcıda: boş kayıt Türkçe alan listesiyle reddedildi, doldurulup kaydedildi, sayfa yenilenince değerler geri geldi,
  `invoicing.seller_profile` satırı doğrulandı.

## Açık kalan

- Bu bilgi girilmeden teslimde taslak açılmaz (`SellerProfileMissingException`); teslim kancası `V1-RMD-472`.
