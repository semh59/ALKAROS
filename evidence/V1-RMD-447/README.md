# V1-RMD-447 - kabul kanıtı

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`;
  46 dosya, 338 test).
- İzinli ve izinsiz oturum: kabuk "Satın alma" sekmesini yalnız `purchasing.manage`, "Üretim" sekmesini yalnız
  `production.manage` olan oturuma listeler. Kayıt yetkisi `requiredCapability` değiştirilince test kırmızı
  (`mutation-permission-gate.log`); dosya geri yüklendi ve `cmp` ile aynı doğrulandı.
- Testler ayrıca: duruma göre görünen düğmeler (taslak sipariş "Gönder", gönderilmiş sipariş "Mal kabul", biten kayıtta hiçbiri;
  planlı parti "Başlat", üretimdeki parti "Tamamla"), ondalık virgül, geçersiz sayı ve boş formun sunucuya gitmeden Türkçe reddi,
  stok kaleminin kendi biriminin siparişe yazılması, mal kabulde açık miktarın varsayılan olması ve yönetici onayı işareti,
  ham enum değerinin ekrana çıkmaması, etkin tarif sürümü yokken partinin oluşturulamaması ve sunucunun Türkçe gerekçesinin
  aynen görünmesi.
- Gerçek deneme (`gercek-deneme.log`, `gercek-deneme-satin-alma.png`, `gercek-deneme-uretim.png`): temiz PostgreSQL veritabanı,
  gerçek Host, tarayıcıdan gerçek giriş. Tedarikçi eklendi (201), 10 kg / 25 TL sipariş oluşturuldu, gönderildi, mal kabul
  kaydedildi (sipariş Tamamlandı; stok 100 kg -> 110 kg). Tarif sürümü seçilerek 10 porsiyonluk parti oluşturuldu, başlatıldı ve
  9,5 porsiyon olarak tamamlandı; hammadde 1,9 kg düştü (108,1 kg), çıktı 9,5 porsiyon çorba olarak stoğa girdi.
- Bilinen sınırlar: tarifler bu görevde yalnız seçilir (tarif ekranı V1-RMD-448); üretim iptali ve sipariş iptali gerçek
  denemede değil yalnız testlerde çalıştırıldı.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla Yönetim > Satın alma'da bir tedarikçi ve sipariş oluşturup gönderin,
  mal kabul edin; Yönetim > Üretim'de bir parti oluşturup başlatın ve tamamlayın. Bu yetkilerden yoksun bir oturumla ilgili
  sekmenin görünmediğini görün.
