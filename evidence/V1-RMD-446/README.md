# V1-RMD-446 - kabul kanıtı

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`;
  44 dosya, 324 test).
- İzinli ve izinsiz oturum ayrı ayrı doğrulanır (`StockSection.test.tsx`): yalnız `reports.view` olan oturum raporları görür,
  stok kalemi ve konum uçlarını hiç çağırmaz, işlem düğmesi görmez; `inventory.manage` ile kalem/konum ekleme, sayım, fire
  ve yeniden sipariş noktası gelir. Yetki kapısı mutasyonu: `canManage` her zaman doğru yapılınca izinsiz oturum testi kırmızı
  (`mutation-permission-gate.log`); dosya geri yüklendi ve `cmp` ile aynı doğrulandı.
- Ondalık virgül (12,5), geçersiz sayının sunucuya gitmeden Türkçe reddi, kalemin kendi birimiyle fire, boş sipariş noktasının
  eşiği kaldırması, ham enum değerinin ekrana çıkmaması ve sunucunun Türkçe gerekçesinin aynen görünmesi testlerle doğrulandı.
- Gerçek deneme (`gercek-deneme.log`, `gercek-deneme.png`): temiz PostgreSQL veritabanı, gerçek Host, tarayıcıdan gerçek giriş.
  Konum ve kalem eklendi (201), 12,5 kg sayım kaydedildi, 99 kg fire sunucudan "Rafta bu kadar stok yok; fire miktarı mevcut
  stoktan büyük olamaz." gerekçesiyle reddedildi (409) ve ekranda aynen göründü, 2 kg fire kaydedildi (stok 10,5 kg),
  yeniden sipariş noktası 20 yapılınca kalem kritik stok raporunda "Kritik" göründü. Gerçek-teorik fark raporu da yüklendi.
- Bilinen sınır: fark raporunun tarih aralığı UTC gün sınırlarıyla sorgulanır (yerel gece yarısı ile 3 saat kayar).
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla Yönetim > Stok'a girin; bir konum ve kalem ekleyip sayım girin,
  stoktan büyük fire girmeyi deneyin ve sunucunun Türkçe uyarısını görün. Aynı ekranı yalnız rapor yetkisi olan bir oturumla
  açınca kalem ve konum araçlarının görünmediğini görün.
