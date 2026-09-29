# V1-RMD-445 - kabul kanıtı

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`;
  42 dosya, 314 test).
- İzinli ve izinsiz oturum ayrı ayrı doğrulanır (`ClosingSection.test.tsx`): yalnız `reports.view` olan oturumda veri
  görünür, "İş gününü aç/kapat", "Ödeme taraması yap" ve vaka geçiş düğmeleri yoktur; `reports.close-day` ve
  `reconciliation.manage` ile düğmeler gelir. `/management` rotası `reports.view` olmadan hiçbir uç çağırmaz
  (`workspace.test.tsx`). Kabuk yalnız oturumun yetkisi olan bölümleri listeler (`ManagementArea.test.tsx`).
- Yetki kapısı mutasyonu: `canManage` her zaman doğru yapılınca izinsiz oturum testi kırmızı
  (`mutation-permission-gate.log`); dosya geri yüklendi ve `cmp` ile aynı doğrulandı.
- Sunucunun Türkçe gerekçesi ekranda aynen görünür (409 örneği testte); ham enum değeri ekrana çıkmaz, Türkçe karşılıkları
  kullanılır; online sipariş vakası bu ekrandan "çözüldü" yapılamaz (sunucu bunu ayrı ekrana yönlendirir).
- Ekran görüntüsü: `yonetim-gun-sonu.png` (yönetici yetkileriyle, sunucu yanıtları taklit edilerek alındı).
- Bilinen sınır: iş günü kapanışında iptal ve yazdırma hatası sayısını sunucu hesaplamıyor, kapanış isteği bunları
  istemciden bekliyor; ekran iki sayı alanı sunar (varsayılan 0) ve bunu açıkça belirtir.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla "Yönetim" sekmesini açıp bir tarih için iş gününü açın ve
  kapatın, sonra bir mutabakat vakasını "İncelemeye al"ın. Aynı ekranı yalnız rapor görüntüleme yetkisi olan bir
  oturumla açınca bu düğmelerin görünmediğini görün.
