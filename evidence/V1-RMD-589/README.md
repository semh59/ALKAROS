# V1-RMD-589 kanıtı

Mutfak ekranında yönetim yetkisi (`kitchen.routing.manage`) olanlar artık yazıcı ekleyebilir ve düzenleyebilir: ad, istasyon, ağ adresi, port, marka profili, aktiflik. Marka profili yazıcı başına tutulur (boşsa ortam değişkeni geçerli) ve gönderimde (kuyruk ve yeniden basım) o yazıcının kendi profili kullanılır. Daha önce yazıcı eklemenin tek yolu veritabanına elle SQL yazmaktı.

- Testler (`test-runs.log`): KitchenOperations 66, Routing 24, PrintQueue 39, PhysicalPrintRecovery 17, ApiConventions 5, Manifest 16 geçti (gerçek Postgres); arayüz testleri, tsc ve lint temiz.
- Mutasyon (`mutations.log`): arka uçta 26 mutantın 23'ü öldü, 1'i ilk koşuda sağ kaldı ve testle kapatıldı (S5), 1'i derlenmediği için düzeltilip öldü (S21). S7 ve S8 (port sınırı) eşdeğerdir: `Printer` varlığı portu zaten 1-65535 dışında reddeder. Arayüzde 15 mutantın 14'ü öldü; U15 (rota bağlantısında izin kontrolünü kaldırmak) eşdeğerdir: ekran düğmeleri ayrıca `canManageRouting` ile kapatılır ve sunucu yetkiyi ayrıca zorlar.
- Adres kuralı: yalnız özel ağ IPv4 adresi (10/8, 172.16/12, 192.168/16); ana makine adı, loopback, bağlantı-yerel ve herkese açık adres reddedilir (sunucunun başka bir servise bağlanmasını önlemek için). Bu kural 192.168.x.x dışında bir yerel ağ kullanan (ör. 100.64/10) kurulumları dışarıda bırakır; böyle bir ihtiyaç doğarsa ayrıca genişletilir.
- Göç listesi sürüm sınırı (`MigrationManifest`), beklenen göç listesi testi ve ekran yolu bağlantı testi V1-RMD-590 ile yapılır (bu görevin izin listesinde değildi); bu görevin commit’inde Manifest testleri bu yüzden kırmızıdır.
- Yazıcı silme ve test fişi bu görevde yoktur. Gerçek yazıcıyla doğrulama yapılmadı: doğrulanamadı (V0-PRN-001 Blocked).
- Önceden var olan sorun (bu görevden değil): PosTerminal tam paketinde `Cashier.*` testleri yük altında zaman aşımına düşüyor; değişiklikler geri alınmış kodda da aynı dosyalar düşüyor (649 testten 6).
