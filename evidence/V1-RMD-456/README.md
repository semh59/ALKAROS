# V1-RMD-456 - kabul kanıtı

- `dotnet build src/Host/ALKAROS.Host.csproj` başarılı. `tests/Host/Experience/Roles` 12/12 (`host-roles-tests.log`) ve
  `tests/Modules/Identity/Authorization` 218/218 (`identity-tests.log`) exit code 0, gerçek PostgreSQL üzerinde.
- Yeni uçlar: `GET /api/v1/management/roles/roles` (rol, izin kodlarıyla), `GET .../roles/permissions` (izin kataloğu) ikisi de
  `identity.roles.manage` ister; `GET /api/v1/management/users` (kullanıcı adı, görünen ad, etkin mi, rol kimlikleri)
  `identity.users.manage` ister. `POST .../roles/roles` artık 204 yerine 200 ve `{ "roleId": ... }` döndürür.
- Testler: her liste ucu için izinli oturum doğru kaydı, izinsiz oturum 403 (Türkçe gerekçe) döndürür; yeni rolün kimliği
  veritabanındaki kayıtla aynıdır; kullanıcı yanıtında "password" geçmez; rota kaydı testi yeni GET yollarını içerir; hizmet
  katmanında aynı izin kuralları ayrıca sınanır.
- Gerçek deneme (`gercek-deneme.log`): temiz PostgreSQL veritabanı, gerçek Host, tarayıcı oturumuyla. Roller ve 37 izin
  listelendi; rol oluşturuldu (kimliği döndü), izin verildi, yeni kullanıcı açıldı ve role atandı; listelerde rolün izni ve
  kullanıcının rol kimliği göründü; kullanıcı yanıtında parola özeti yok.
- Not (V1-RMD-457 için): izin kataloğundaki `name` alanları İngilizce (ör. "Zero-price a delivered item"); ekran bunları
  göstermeden Türkçe bir sözlükten çevirmelidir. Rol `name` alanı kullanıcıya ait bir metindir.
