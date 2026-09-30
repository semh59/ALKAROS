# V1-RMD-450 - kabul kanıtı

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`;
  52 dosya, 385 test). Eski `/settings/security` ekranının 6 testi değişmeden geçiyor.
- İzinli ve izinsiz oturum: kabuk "Güvenlik" sekmesini yalnız `security.manage`, "Sistem sağlığı" sekmesini `reports.view` olan
  oturuma listeler; uyarı işlem düğmeleri (Gördüm, Üst seviyeye taşı, Sustur, Çözüldü) yalnız `observability.manage` olan oturuma
  görünür. Güvenlik sekmesinin yetkisi değiştirilince test kırmızı (`mutation-permission-gate.log`); dosya geri yüklendi, `cmp` aynı.
- Testler ayrıca: hesap arama, oturum sonlandırma, kilit kaldırma (kilitli değilse düğme kapalı), bulunamayan kullanıcı, bakım
  işlerinin Türkçe durumu ve kapalı işin çalıştırılamaması, yedek kapsamının açığı gizlememesi, sipariş yığınının önce önizleme
  sonra ayrı onayla kapatılması, tanılama paketi formu doğrulaması ve sunucunun Türkçe gerekçesinin aynen görünmesi, uyarıda
  güncel sürüm numarasının gönderilmesi, çözümde gerekçe zorunluluğu, ham enum değerinin ekrana çıkmaması.
- Gerçek deneme (`gercek-deneme.log`, `gercek-deneme-guvenlik.png`, `gercek-deneme-sistem.png`): temiz PostgreSQL veritabanı,
  gerçek Host, tarayıcıdan gerçek giriş. Kullanıcı bulundu ve oturumları sonlandırıldı (200); bakım işi çalıştırıldı (200); yedek
  kapsamı "Hedefin dışında / Yedek makbuzu yok" olarak açıkça görüldü; sipariş yığını önizlemesi çalıştı (0 sipariş); tanılama
  paketi oluştu; uyarı görüldü olarak işaretlendi, gerekçesiz çözme reddedildi, gerekçeyle çözüldü. Eski `/settings/security`
  adresi aynı bileşenle açılıyor.
- Bilinen sınırlar: gerçek denemede kapatılacak eski sipariş bulunmadığından "kapat" adımı yalnız testte çalıştırıldı; kilitli
  hesap gerçek denemede yok (testte var). Sır döndürme ve giden kutusu uçları (aynı grupta) görev metninde yok, bu bölüme alınmadı.
  Eski ekranın istemci fonksiyonları (`api.lookupUser` vb.) artık bu ekran tarafından kullanılmıyor; paylaşılan dosyalar bu görevin
  dışında olduğundan silinmedi.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla Yönetim > Güvenlik'te bir personeli arayıp oturumlarını sonlandırın,
  bir bakım işini çalıştırın; Yönetim > Sistem sağlığı'nda bir uyarıyı görüldü işaretleyin. `security.manage` izni olmayan bir
  oturumda Güvenlik sekmesinin görünmediğini görün.
