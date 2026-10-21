# qa-entegrasyon-cihaz (dar kapsamlı) — V1-RMD-597 ham rapor özeti

20/20 birincil kalem (DeviceSessions 7 dosya, OfflineReconciliationEndpoints.cs, auth.js, kiosk-lock.js, offline-queue.js, sw.js ve bağlı offline motoru 8 dosya). Test/derleme koşulmadı. Kanıt düzeyi Kod okuması (aksi belirtilmedikçe).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| F1 | Yüksek | kiosk-lock.js:224 `if (result.status === 423) showLogin();`, auth.js:118 `localStorage.setItem('alkaros_waiter_locked','0')`, waiter-app.js:865-871 init, DualScreenApplication.Endpoints.cs:202-222 | 5 yanlış PIN sonrası 423'te showLogin() kalıcı kilit bayrağını 0 yapar, sunucu oturumu iptal edilmez; sayfa yenilenince /auth/session 200 döner ve kilit gelmez. Kiosk PIN kilidi yenilemeyle aşılır. E2E 07-kiosk-lock.spec.js:241 yenilemeyi denemez. |
| F2 | Orta | offline-queue.js:371-375 | Kuyruk `!result.ok` bakılmadan silinir: 429/500/503'te tüm çevrimdışı yetkili işlemler (bills.void/comp) cihazdan silinir; sunucuda hiç oluşmaz. |
| F3 | Orta | offline-queue.js:228-232, api.js:48, failed-orders.js:58 | Çevrimdışı sipariş kuyruğu boşalırken 401/403/409/423 siparişleri kalıcı "hatalı" listesine taşır; yeniden deneme yok, yalnız "Sil". |
| F4 | Orta | waiter-app.js:144-146 | Çıkış çağrısı ağ hatası verirse sunucu oturumu (8 saat) açık kalır, istemci çıkmış görünür; sonraki garson önceki kullanıcının oturumuna girer. |
| F5 | Orta | state.js:137, Endpoints.cs:144-152, kiosk-lock.js:129 | PIN kurulu bilgisi yalnız yerel bayrakta; çıkış/depolama temizliği/yeni cihazda 3 dk boşta kilidi sessizce kapalı. |
| F6 | Orta | OfflineGrantReconciler.cs:106, PostgresOfflineReplayLedger.cs:~165 | OfflineAuthorizedAt istemci saatinden gelir, alt sınır/sunucu doğrulaması yok; 4 saatlik bütçe süresi sunucuda uygulanamaz (yönetici incelemesi korur). |
| F7 | Orta | OfflineGrantReconciler.cs:113-115,149,120, state.js:250-257 | Sayım kontrolü ile kayıt atomik değil (eşzamanlı istek MaxCount'u aşabilir); canlı politika kontrolü önceki sayı 0 ile çağrılır (yalnız AlwaysDeny yakalanır); her giriş sıfır sayaçlı yeni bütçe verir. |
| F8 | Orta | void-comp.js:100,32,114 | Kalem-anahtar eşlemesi bellekte; yenilemeden sonra aynı kalem için yeni anahtar → çift grant (çift yönetici onayı). |
| F9 | Düşük | OfflineReconciliationEndpoints.cs:49-51,79 | Hız sınırı ve toplu istek üst sınırı yok; Actions null ise NRE/500. |
| F10 | Düşük | OfflineReconciliationEndpoints.cs:75-80 | Bütçe yalnız UserId ile eşleşir, terminale/oturuma bağlı değil. |
| F11 | Düşük | Endpoints.cs:~84, DualScreenStore.cs:44-56 | terminalId istemcinin seçtiği GUID; geçerli kimlik bilgisiyle sınırsız terminal kaydı (cihaz kaydı yok). Karar gerekir. |
| F12 | Düşük | Endpoints.cs:~207-213, DualScreenStore.cs:66-81, DeviceSessionService.cs:52 | Doğru PIN oturumu her seferinde 8 saat yeniler (mutlak üst sınır yok); kasiyer oturumunda last_seen_at yazılmaz; servis varsayılanı 30 gün. Karar gerekir. |
| F13 | Düşük | PostgresDeviceSessionRepository.cs:123, DeviceSessionService.cs:105,206 | Yeniden bağlanma operasyon defteri üretimde çağrılmıyor (ölü kod); `session_operations` boş. |
| F14 | Düşük (Çıkarım) | sw.js:157,165-174,160-162 | /hubs/ GET'leri 3 sn'de koparılır; önbellek kaçırılırsa respondWith(undefined); cache.put waitUntil'siz; kuyruk şemasında sürüm yok. Cihaz koşulunda doğrulanamadı. |
| F15 | Düşük | DeviceSessionTokenTests.cs:19-23 | Test adı "LowercaseHexHash", ifade büyük harf. |

Ek: reddedilen çevrimdışı işlem için kalem bazlı geri bildirim yok; bekleyen çevrimdışı eylemler şeritte sayılmıyor (state.js:224-231).
Temiz: belirteç 32 bayt CSPRNG, yalnız SHA-256 saklanır; yeniden bağlanma talebi FOR UPDATE; AuthenticateCashierAsync belirteç+cihaz+iptal+süre+aktiflik denetler; uzlaştırma kimliği/rolü sunucu gerçeği; uzlaştırma yazımı tek işlem; PIN ayrı sayaç ve hash; /api/ önbelleğe alınmaz; V1-RMD-404/406/427 düzeltmeleri gerilememiş (test adlarıyla örtüşür).
Doğrulanamayan: fiziksel cihaz davranışları (tam ekran çıkışı, ekran sabitleme, WakeLock, HTTP altında SW, WebSocket engelli ağ); E2E 07 ve 10 sonuçları; F7(b) pencere sayacı anlamı; F11/F12 ürün kararı.
