# V1-RMD-455 - kabul kanıtı

- `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`; 43 dosya,
  316 test). `Cashier.management-link.test.tsx`: `reports.view` olan oturumda ana ekran üst çubuğunda "Yönetim" bağlantısı
  `/management` adresine gider; yetkisiz oturumda görünmez.
- Gerçek deneme: temiz PostgreSQL veritabanı, gerçek Host, tarayıcıdan gerçek giriş. Ana ekranda bağlantı 1 adet; basınca
  gün sonu ekranı açıldı, "İş gününü aç", "Ödeme taraması yap" ve "İş gününü kapat" çalıştı (`gercek-deneme.png`).
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla giriş yapın; ana Kasa ekranının üst çubuğunda "Yönetim"e
  basın ve gün sonu ekranının açıldığını görün.
