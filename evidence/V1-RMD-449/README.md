# V1-RMD-449 - kabul kanıtı

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`;
  50 dosya, 369 test). `src/stale.test.ts` bu görevden bağımsız olarak aralıklı kırmızı oluyor (tam takımda yaklaşık üçte
  ikisi geçiyor, tek başına hep kırmızı; değişiklikler geri alınmış temiz kodda da aynı).
- İzinli ve izinsiz oturum: kabuk "Personel ve roller" sekmesini yalnız `identity.users.manage` olan oturuma listeler; "Personel
  bul / Pasifleştir" araçları yalnız `security.manage` olan oturuma görünür. Kayıt yetkisi değiştirilince test kırmızı
  (`mutation-permission-gate.log`); dosya geri yüklendi ve `cmp` ile aynı doğrulandı.
- Testler ayrıca: kısa parola ve boş adın sunucuya gitmeden Türkçe reddi, hesap açıldıktan sonra formun temizlenmesi, sunucunun
  Türkçe gerekçesinin aynen görünmesi (aynı kullanıcı adı, kendi hesabını pasifleştirme), bulunamayan kullanıcı, pasifleştirme
  sonrası durumun yeniden okunması.
- Gerçek deneme (`gercek-deneme.log`, `gercek-deneme-personel.png`): temiz PostgreSQL veritabanı, gerçek Host, tarayıcıdan gerçek
  giriş. Kısa parola reddedildi; hesap açıldı (200); aynı ad tekrar denenince sunucunun Türkçe gerekçesi göründü (409); kullanıcı
  bulundu, pasifleştirildi ve yeniden etkinleştirildi (200/200); olmayan ad için açıklama göründü.
- Bilinen sınır (önemli): sunucuda rol, izin ve kullanıcı LİSTELEME ucu yok ve rol oluşturma yeni rolün kimliğini döndürmüyor.
  Bu yüzden bu görevde yalnız mevcut uçlarla yapılabilen kısım gerçekleşti (hesap açma, kullanıcı bulma, pasifleştirme,
  yeniden etkinleştirme). Rol ve izin yönetimi ile rol atama için önce sunucu uçları gerekir; ekranda bu açıkça yazıyor.
  Süreli yetki devri zaten "Yetki kararları" ekranında. Yeni hesapla giriş bu denemede doğrulanmadı.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla Yönetim > Personel ve roller'de bir personel hesabı açın, adıyla
  bulup pasifleştirin ve yeniden etkinleştirin. Bu izinlerden yoksun bir oturumla sekmenin (ya da bulma aracının) görünmediğini görün.
