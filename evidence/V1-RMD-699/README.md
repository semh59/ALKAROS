# V1-RMD-699 - İstemci erişilebilirlik ve doküman sapmaları

## Ne değişti

- **Bekleyen hesap kasada görünür (B068):**
  - Bekleyen hesaplar ekranı, ilk yüklemeden sonra daha önce görülmemiş bir hesap gelince ses çalıyor (açılışta zaten bekleyenler sessiz).
  - Canlı bağlantı koptuğunda "Canlı güncelleme yok" şeridi çıkıyor; yenileme başarısız olursa liste "eski olabilir" etiketi ve son yenileme saatiyle gösteriliyor.
  - Başka ekranda iken (`usePendingChecksAlert`) üst çubuktaki "Bekleyen hesaplar" bağlantısında bekleyen sayısı rozeti ve yeni hesapta ses var.
- **Dokunma hedefleri (B127):** Kasa, mutfak ve bekleyen hesap ekranlarındaki düğmeler ve alanlar en az 48 piksel (önceden 36-46).
- **Arka perde erişilebilirliği (B128):** Eşleştirme penceresinin arka perdesi `role="presentation"`; klavye tuzağı arka perdede; kategori şeridi `nav` yerine `div role="tablist"`. oxlint'te bu üç uyarı kalktı.
- **Sabit beklemeler (B123):** AnalysisRunner/InsightRunner (5 sn ve 1,5 sn), KitchenOperations yeniden yazdırma, Yemeksepeti iptal ve QR kabul/ret testleri artık koşul bekliyor (kilit bekleyen oturum `pg_stat_activity`'den, sayaç veya alınan metin yoklanarak); 30 sn üst sınır.
- **Host belgeleri ve yorumlar:**
  - B131: topoloji belgesi — SignalR yükü, çerezin `Secure` koşulu, izin kodu yerine çerez oturumu, "uygulama görevi yok" paragrafı tarihsel not oldu.
  - B135: bağlayıcı `http://api:5080` (deploy README, Dockerfile, test yorumu), .NET 10 / net8.0 durumu uyumluluk matrisinde.
  - B137: `WaiterPresenceTracker` yorumu, çıkış kodu 3.
  - B139: kullanım metni `watchdog` ve `--operator` ile tamamlandı.
  - B140: `order.json` `phaseBRange.max` 201 ve `MigrationManifest.PhaseBMax` ile eşitlik testi.
  - B141: bağımlılık grafiği (pozisyonlar), `settings-ownership.md` (197), rehearsal profili (195), `TableOccupancyChanged` henüz yayımlanmıyor.
- **İzin kodu kataloğu (B143):** `ApplicationPermissions.MigrationSeededCodes` (11 kod) ve sınıf özeti; her kodun bir göçte tohumlandığını ve katalogla çakışmadığını test ediyor.

## Kanıt

- `mutate.mjs` / `mutations.log`: 15 mutant, hepsi KILLED. İlk koşuda S1 ve S2 (kullanım metni) SURVIVED: test, açıklama satırlarında da geçen kelimeleri arıyordu; test tam sözdizimi satırlarını arayacak şekilde sıkılaştırıldı, yeniden koşuda KILLED (log'da iki satır).
- Test koşuları: istemci vitest 81 dosya/750 test (+ yeni testlerle), `tsc` temiz; Host.Tests 277, Identity.Authorization 247, mimari 5+11; bekleme düzeltmesi yapılan beş proje yeşil. Kayıtlar `logs/` altında.

## Yapılmayanlar / doğrulanamadı

- **B122, B133:** görev dışı (S9 ve V1-RMD-430).
- **B128 mutasyonu:** arka perde değişikliği yalnızca lint ile doğrulandı (uyarı kalktı); davranış testi yok, ayrı mutant üretilmedi.
- **B123:** bekleme düzeltmeleri davranış değiştirmediği için mutantla ölçülemez; kanıt, değişen satırlarda sabit bekleme kalmaması ve testlerin yeşil koşusu. Kalan `Task.Delay` kullanımları döngü içi yoklama, yavaş iş taklidi veya "olmadığını" doğrulayan pencerelerdir.
- **Uyumluluk matrisi işletim sistemi satırı (Windows 11) ve `docker self-heal`:** doğrulanamadı, değiştirilmedi.
- **Göç yorumları (101 ve 100):** göç dosyaları sağlama toplamı nedeniyle değiştirilemez; yanlış yorum bu yüzden duruyor.
- **`ScreensaverImageStatus` ölü kayıt ve DualScreenOptions relay yorumu:** `src/Host/DualScreen/**` bu görevin yüzeyi dışında; bulgu açık.
- **oxlint:** projede görev öncesinden `CustomerDisplay.tsx:244` (`Date.now`) hatası var; bu görevde dokunulmadı.
- **Design-system diyalog uyarıları** (`primitives.tsx`): görev yüzeyi dışı.
