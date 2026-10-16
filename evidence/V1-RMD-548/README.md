# V1-RMD-548 - Kullanıcı yolculuğu ekranı

Ne yapıldı
- Sunucu: `/api/v1/management/observability/journeys` altında 4 okuma ucu: `actions` (düğme kodu başına sayı/ortalama/en yavaş/hata, rol ve cihaz süzgeci, son N saat), `requests` (en yavaş / en hatalı / en yeni, ekranın kendi çağrıları hariç), `requests/{izlemeKimliği}` (tek eylemin zaman çizelgesi), `staff-movements` (personel hareketleri).
- Zaman çizelgesi: istek + veritabanı + dış çağrı adımları bellekteki izleme tamponundan, olaylar `audit.audit_events` tablosundan aynı izleme kimliğiyle gelir; tek listede sunucu saatine göre başlangıç sırasıyla.
- Yetki: tüm uçlar `reports.view`; `staff-movements` ayrıca `observability.manage` ister (görüntüleme yetkili vardiya sorumlusu 403 alır).
- Ekran: yönetim paneline "Kullanıcı yolculuğu" sekmesi (İstekler / Düğmeler / Personel hareketleri); teknik terimler ekranın üstünde açıklanır; personel sekmesi yalnız `observability.manage` ile görünür.

Kabul koşulları
1. Seçilen eylemin adımları tek izleme kimliğiyle sıralı: `TimelineOrdersTheRequestItsDatabaseCallsAndItsEventsUnderOneTraceId` (gerçek Postgres, gerçek izleme tamponu, gerçek Npgsql etkinliği; başka izleme kimliğinin olayı karışmaz).
2. Yetkisiz rol 403: `CallsWithoutASessionAreUnauthorizedAndWithoutPermissionAreForbidden` (oturumsuz 401, yetkisiz 403, 3 uçta) ve `ViewOnlySupervisorSeesJourneysButNotStaffMovements`.
3. Bu klasör. Testler: Host 8/8, PosTerminal tamamı 599/599 (test-runs.log). Mutasyonlar: mutations.log, 15 mutantın hepsi sonunda yakalandı (iki tanesi ilk turda sağ kalmış, testler güçlendirilip yeniden öldürüldü).

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- Düğme tıklaması izleme kimliği taşımıyor, bu yüzden zaman çizelgesinde ilk adım olarak görünmez; düğme istatistiği ayrı sekmede. Bağlama V1-RMD-568'e eklendi.
- İstek adımları sunucu belleğinden gelir: sunucu yeniden başlayınca veya 200 istekten sonra silinir; ekran bunu söyler. Olaylar kalıcıdır.
- Rol/cihaz süzgeci yalnız düğme istatistiğine uygulanır (istek kaydında rol/cihaz yok). Cihaz kimliği elle yazılır; seçim listesi yok.
- Adım adlarında teknik kodlar (adres, olay adı, sorgu biçimi) olduğu gibi gösterilir; Türkçe çeviri sözlüğü yok.
- Tarayıcıda gerçek çalıştırma yapılmadı (yalnız jsdom testleri).
- Bu görevden bağımsız: `tests/Architecture/ApiConventions` `EveryMutatingEndpointTakesAnIdempotencyKey` testi, önceki görevlerin 4 POST ucu yüzünden (errors/acknowledge, errors/resolve, telemetry/browser-errors, telemetry/interactions) başarısız; bu görevin uçları yalnız GET.
