# V1-RMD-172 - Çevrimdışı yeniden yükleme artık kuyruğu kilitlemiyor

- Task ID: V1-RMD-172
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümünden iki
Medium bulguyu kapatır.

1. **Çevrimdışıyken yeniden yükleme kuyruğu kullanılamaz giriş ekranının
   arkasında bırakıyor.** `init()`, `hasValidSession()` başarısız olduğunda
   (sunucuya HİÇ ulaşılamadığı durum dahil) doğrudan tam ekran giriş
   overlay'ini gösteriyordu. Ama genuinely çevrimdışıyken bu çağrı
   sunucuya ulaşamaz — "oturum geçersiz" ile "şu an kontrol edilemiyor"
   aynı şeye indirgeniyordu. Gerçek oturum sunucu tarafında muhtemelen
   hâlâ geçerliyken, ve gönderilmemiş sipariş kuyruğu localStorage'da
   güvenle dururken, garson uygulamaya çevrimiçi olup TEKRAR giriş
   yapana kadar erişemiyordu. `hasValidSession()` artık üç durumlu:
   `'yes'`/`'no'`/`'offline'` — yalnız gerçekten cevap veren bir sunucudan
   gelen 401/403 giriş ekranını tetikliyor; çevrimdışı durumda uygulama
   iyimser şekilde `start()`'a devam ediyor (her `load*` çağrısı zaten
   kendi başarısız durumunu zarifçe boş listeye düşürüyor, çökmüyor) —
   ribbon ve kuyruk düğmesi hiçbir şeyin arkasında kalmadan görünür ve
   çalışır durumda. Gerçekten süresi dolmuş bir oturum, `api()`'nin zaten
   var olan 401 yakalayıcısı sayesinde bir sonraki gerçek çağrıda hâlâ
   giriş ekranına düşer — güvenlik açığı değil. Ağ geri geldiğinde
   (`online` olayı), kullanıcı bilgisi henüz doldurulmadıysa oturum
   tekrar kontrol ediliyor ve gerçek veriler (bölgeler/katalog/masalar/
   bekleyenler) bir kez daha yükleniyor.
2. **Düz HTTP'de şerit yanlış sebebi söylüyor.** `renderRibbon()`,
   `state.offlineDisabled` true olduğunda HER ZAMAN "güvenli bağlantı
   (HTTPS) gerekli" diyordu — bu üç ayrı kök nedenden yalnız biri için
   doğruydu (gerçekten güvensiz bağlantı). Tarayıcı servis işçisini hiç
   desteklemiyorsa veya kayıt başka bir sebeple başarısız olduysa (sw.js'e
   ulaşılamaması gibi), bağlantı zaten güvenli olsa bile mesaj hâlâ HTTPS'i
   suçluyordu. Artık üç ayrı, doğru Türkçe mesaj var
   (`OFFLINE_DISABLED_REASONS`), `registerOfflineWorker()` gerçek nedeni
   `state.offlineDisabledReason`'a yazıyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-172-offline-reload-and-ribbon-reason.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — `hasValidSession` üç durumlu, `init`/`online` olayı güncellendi;
    `OFFLINE_DISABLED_REASONS`, `registerOfflineWorker` üç dala ayrıldı.

## Out of scope

Frontend'in kalan bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-171

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- Bu görev için ayrı bir otomatik test eklenmedi (repoda bu dosya için JS
  test altyapısı yok) — kod gözden geçirildi:
  - `hasValidSession()`'ın tek çağıranı (`init()`) ve tek başka kullanım
    yeri (`online` olay dinleyicisi) güncellendi; eski boole dönüş
    değerine dayanan başka çağıran kalmadığı grep ile doğrulandı.
  - `api()`'nin `offline: true` alanının yalnız gerçek bir `fetch()`
    hatasında (ağ düzeyi başarısızlık) set edildiği, normal 4xx/5xx HTTP
    yanıtlarında set edilmediği kaynaktan teyit edildi — `result.offline`
    kontrolü doğru ayrımı yapıyor.
  - `api()`'nin kendi 401 yakalayıcısının (`if (response.status === 401)
    showLogin();`) hâlâ olduğu gibi durduğu, dolayısıyla gerçekten süresi
    dolmuş bir oturumun bir sonraki gerçek çağrıda hâlâ yakalandığı
    doğrulandı — iyimser devam etmek bir güvenlik gevşemesi değil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyada 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
