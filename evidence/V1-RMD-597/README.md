# V1-RMD-597 — Identity kalite denetimi

HEAD f4afb5e6, 231 dosya. Yöntem: 13 bağımsız ajan → 257 ham satır → 40 benzersiz kök neden (findings-raw.md) → 7 bağımsız doğrulayıcı (verifier/verifier.md) → bulgu kayıtları (findings.json). Koşu kayıtları yeşil: 333 .NET test + 23 vitest (runs/). Test-kalitesi ajanı 98 mutant denedi: 44 öldü, 46 hayatta kaldı (bulgu B028).

## Sayılar (doğrulayıcı sonrası)
Kritik 0 · Yüksek 3 · Orta 21 · Düşük 16 (toplam 40). Hiçbir bulgu tamamen çürütülmedi; 20'sinin önemi düştü, 7 alt madde çürüdü (verifier.md sonu). Ajan başına tablo: agents/README.md.

## Yüksek bulgular
- B001 PIN kilidi yalnız tarayıcıda; 5 yanlış PIN sonrası sayfa yenilenince kilit kalkar (kiosk-lock.js:224, auth.js:118).
- B002 Rol/izin/kullanıcı yönetimi hiçbir denetim kaydı yazmaz (RoleManagementService.cs:72-131).
- B009 Temsil limiti tutarı yanlış karşılaştırır: indirim yüzdesi TL ile, iptal tutarı hep 0 (PostgresAuthorizationDelegationRepository.cs:78, OrderManagementEndpoints.cs:782).

## Temiz bulunan alanlar
PBKDF2 600k + sabit zamanlı karşılaştırma, tek hata mesajı, atomik sayaçlar, 32 baytlık oturum belirteci (yalnız özet saklanır), çerez bayrakları, SQL parametreli, kendi talebini onaylama (Host düzeyinde, HTTP testli), 26 izin kodu ↔ DB ↔ istemci etiketi birebir, göç zinciri 193 up/193 down/193 id ve 39 down round-trip şema farkı yok, tohum idempotent.

## Doğrulanamayanlar
Giriş/PIN gecikmesi ölçülmedi; gerçek eşzamanlı yarış koşulmadı; fiziksel cihaz davranışları; EXPLAIN ile indeks iddiaları; üretim collation'ı; Docker kaynak IP (X-Forwarded-For); housekeeping zamanlaması; vitest mutasyonu; tsc/oxlint/axe koşulmadı.

## Fırsat tablosu
| ID | Fırsat | Etki |
|---|---|---|
| 597-F001 | Girişte pinSet döndür, kilit bayrağını sunucu bilgisinden türet | Vardiya başı 5+ dokunuş azalır |
| 597-F002 | Kilit ekranında kullanıcı adı ("Merhaba Ayşe") | Paylaşılan tablette karışma azalır |
| 597-F003 | Kasa/yönetim ekranına boşta kilit + PIN ile hızlı vardiya geçişi | Açık unutulan kasa riski |
| 597-F004 | Garsona onay/ret sonucu bildirimi | Garson beklemede kalmaz |
| 597-F005 | Personel listesini yazmadan seçilebilir yap + durum rozeti | Yönetici arama yazmaz |
| 597-F006 | Zayıf PIN (0000, 1234) reddi | PIN tahmini zorlaşır |
| 597-F007 | Hesap açarken rol seçimi aynı formda | Yeni çalışan ilk gün kullanır |
| 597-F008 | Çevrimdışı bütçe süresini vardiyayla hizala | Vardiya ikinci yarısında çevrimdışı yetki |
| 597-F009 | Yetki karar ekranı arka planda otomatik yenilenir | Yönetici elle yenilemez |

## Bulgu → düzeltme görevi haritası (görevler 597 kapandıktan sonra açılır)
- V1-RMD-667 PIN/kilit zinciri: B001, B003, B004, B019, B024, B032 (+F001, F002, F006)
- V1-RMD-668 Yönetim denetimi: B002, B020, B022, B026
- V1-RMD-669 Yetki karar ve personel ekranı: B005, B006, B029 (+F004, F005, F007, F009)
- V1-RMD-670 Limit tutarı ve onay yetkisi: B009, B008, B010, B013, B015, B017
- V1-RMD-671 Çevrimdışı zincir: B007, B011, B012, B014, B023, B030
- V1-RMD-672 Dayanıklılık/ops/doküman/test: B018, B021, B025, B027, B028, B031, B035–B040
- PO kararı gerekir: B016 (otomatik onay limit değerleri), B033 (kişisel ikram bütçesi 50/150 TL belgesi, erişim kaydı süresi), B034 (npm lisansları + Npgsql).
Kritik/Yüksek (B001, B002, B009) 598 başlamadan kapanacak.

## Semih için (≤10 satır)
Identity denetimi bitti: 40 bulgu, 3'ü Yüksek, Kritik yok. En önemlisi: 5 yanlış PIN'den sonra kilit sayfa yenilenince kalkıyor ve yönetici PIN kilidini açamıyor. Rol/izin değişiklikleri hiçbir kayıt bırakmıyor. İndirim limiti tutarı yanlış karşılaştırıyor. Hepsi düzeltilecek (667–672). Senin kararın gereken üç konu var: otomatik onay limit değerleri, kişisel ikram bütçesi, Npgsql/npm lisansları.

## Semih senaryosu
Garson indirim ister → yönetici onaylar: ekran çalışır ama hata mesajları görünmez ve talep eden kişi adı yerine kod görünür (B005, B006). Aynı kişi kendi talebini onaylayamaz: sunucuda çalışır (testli), ekranda sessiz kalır. 5 yanlış PIN kilitler, yönetici açar: PIN kilitlenir ama yönetici göremez ve açamaz; geçici yol 15 dk veya şifreyle girip PIN'i yeniden kurmak (B004, B001).
