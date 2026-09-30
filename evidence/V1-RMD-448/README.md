# V1-RMD-448 - kabul kanıtı

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`;
  49 dosya, 360 test). `src/stale.test.ts` tek başına çalıştırılınca bu görevden bağımsız olarak kırmızı (değişiklikler geri
  alınmış temiz kodda da aynı); tam takımda geçiyor.
- İzinli ve izinsiz oturum: kabuk "Menüler" sekmesini yalnız `menu.manage`, "Tarif maliyeti" sekmesini yalnız `inventory.manage`
  olan oturuma listeler. Menü kaydının yetkisi değiştirilince test kırmızı (`mutation-permission-gate.log`); dosya geri
  yüklendi ve `cmp` ile aynı doğrulandı.
- Testler ayrıca: menü pasifleştirme, menü kalemi ve katalogda pasif ürün uyarısı, geçersiz sayının sunucuya gitmeden Türkçe
  reddi, günün menüsü yokken oluşturma, taslak/açık/kapalı duruma göre görünen düğmeler (kapalı menüde düzenleme yok), fiyat ve
  porsiyon değiştirme, boş fiyatın katalog fiyatı olması, yalnız etkin tarif sürümünün seçilebilmesi, ham enum değerinin ekrana
  çıkmaması, sunucunun Türkçe gerekçesinin aynen görünmesi, maliyet kaydı yokken açıklama ve maliyet isteğinin her malzemenin
  stok birimini göndermesi (`api.test.ts`).
- Gerçek deneme (`gercek-deneme.log`, `gercek-deneme-menuler.png`, `gercek-deneme-tarif-maliyeti.png`): temiz PostgreSQL
  veritabanı, gerçek Host, tarayıcıdan gerçek giriş. Menü ve ürün eklendi, günün menüsü oluşturuldu, tarifli ürün eklendi,
  geçersiz fiyat reddedildi, fiyat 52,50 TL yapıldı, menü açıldı ve kapatıldı (hepsi 200/201). Tarif maliyeti: 2 kg un x 25 TL =
  parti 50 TL, porsiyon 5 TL.
- Gerçek deneme bir hata yakaladı: ilk sürüm maliyet isteğinde malzemelerin stok birimini göndermiyordu, sunucu 400 döndü;
  istemci artık tüm stok kalemlerinin birimini gönderiyor ve regresyon testi eklendi.
- Konsoldaki 404'ler beklenen durumlardır (o gün için menü yok, o tarih için kayıtlı maliyet yok); ekranda açıklama olarak gösterilir.
- Bilinen sınırlar: menü kalemi sıralaması (yeniden sıralama) ve tarif oluşturma bu görevde yok; maliyet yalnız bugünün tarihi için
  hesaplanır, geçmiş tarih yalnız görüntülenir.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla Yönetim > Menüler'de menü ve günün menüsünü oluşturup açın; Tarif
  maliyeti'nde bir tarif sürümü seçip "Bugünkü maliyeti hesapla"ya basın. Bu yetkilerden yoksun bir oturumla sekmelerin görünmediğini görün.
