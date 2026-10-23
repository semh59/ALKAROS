# V1-RMD-695 - Modül sınırı, kayıt ve mimari test

## Ne değişti

- **Mimari kenar testi (B025):** `ApprovedHostOrchestrationEdges` her Host alanını kapsıyor (28 yeni giriş, her biri o günkü gerçek modül bağımlılığına sabitlendi); `Secrets` ve `QrRelay` de denetleniyor (önceden listede adı geçip hiç denetlenmiyordu). Yeni test `EveryHostAreaIsCoveredByAnApprovedEdgeEntry` listede olmayan her Host alanını raporluyor; giriş noktası ve ortak Experience kablolaması tasarım gereği kök sayılıyor.
- **Kompozisyon testi (B026, B101):** onaylı dekoratör girdilerinin hâlâ bir modülce kaydedildiğini, iki modülün aynı hizmeti aynı uygulama ve aynı ömürle bildirdiğini, ve bildirilen ömrün (singleton / kapsamlı) sunum kabında tuttuğunu denetliyor.
- **Ölü kod (B090):** `AuthenticateDisplayAsync`'in hiç kullanılmayan `requiredDisplayId` parametresi ve SQL kolları, kullanılmayan using, giriş ucundaki hep doğru dal silindi.
- **Sipariş numarası (B091):** İstanbul yerel tarih ve saatini taşıyor (`DualScreenStore.OrderNumber`).
- **Açılış (B093, B094):** `serve` mutfak istasyonu tanımsızsa başlangıçta `STARTUP:` hatasıyla duruyor (önceden ilk siparişte 500); meşgul port gibi sunucu başlatma hataları aynı yoldan raporlanıyor; sürüm kaydı hatası daha geniş yakalanıyor.
- **Bağlantı dizesi (B095):** veritabanı URL'sinde kullanıcı ve veritabanı adı çözülüyor.
- **Saat (B097):** yardım çağrısı bekleme süresi ve ekran eşleştirme/oturum bitişleri tamamen veritabanı saatiyle.
- **Yorumlar (B049, B105):** oturum sorgusunun yazma yaptığı ve modül kurallarını kopyaladığı, `DualScreenStore`'un sipariş deposunu neden kendisinin kurduğu doğru anlatılıyor.

## Kanıt

- `mutate.mjs` / `mutations.log`: 12 mutant. İlk geçişte H1 (bekleme süresi birimi), P1–P3 (eşleştirme/oturum süreleri), D1 (giriş yanıtında rol adı) sağ kaldı; testler eklendi (eşleştirme akışı, süre dolması, bekleme süresi yaşlandırma), ikinci geçişte hepsi KILLED. A4 (Secrets'ı denetlenen kümeden çıkarma) yerine A3 kullanıldı: aynı alanın onayından Secrets çıkarılınca test kırılıyor.
- Yeni testler: `DatabaseUrlTests`, `StartupRequirementTests`, `OrderNumberTests`, eşleştirme testleri (`DualScreenStoreTests`), yardım çağrısı süre testi, mimari kapsam testi, bileşim ömür testi.
- Tam test koşusu `logs/` altında.

## Yapılmayanlar / doğrulanamadı

- **B048 (anonimleştirme durum atlaması):** değiştirilmedi. Saklama işi, istek kaydını hangi durumda olursa olsun `Anonymized` yapıyor; durum makinesi yalnız `Pending -> Anonymized` izin veriyor ve "CustomerAnonymized" denetim olayı yazılmıyor. Kod PO onaylı tasarım olarak yorumlanmış; davranışı değiştirmek istek kayıtlarını `RetentionBlocked`'ta bırakabilirdi. Ayrı karar.
- **B049/B050:** `device_sessions.last_seen_at` yazımı ve `table_mgmt.tables` yazımları Host'ta kaldı (modül portuna taşınmadı); B049 için yalnız yorum düzeltildi.
- **B100:** Experience alanlarındaki yedek `TryAdd` kayıtları silinmedi: bağımsız kompozisyon testleri onlara dayanıyor ve çoğu dosya bu görevin yüzeyi dışında.
- **B104:** giriş ucundaki sekiz ardışık çağrı ve iş mantığı yerinde; yalnız ölü dal silindi.
- **B093:** `RegisterAsync().GetAwaiter().GetResult()` eşzamanlı bekleme duruyor (tembel singleton fabrikasında); testi yok.
- **B099:** RefundIntentsModule kaydı kapsam dışı (V13-HUG-003).
- Mimari kapsam testi `docs/architecture/module-dependency-rules.md`'yi güncellemiyor (yüzey dışı).
