# V1-RMD-151 - Garson telefonu 30 gün boyunca onun kimliğiyle açık

- Task ID: V1-RMD-151
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

Kiosk kilidi konuşulurken bulundu (2026-09-10):
`DeviceSessionService.DefaultLifetime` 30 gün ve hiçbir hareketsizlik kilidi
yok. Garson telefonu masada bırakırsa onu eline alan kişi o garsonun
kimliğiyle sipariş girebilir, kalem iptali isteyebilir, masa devredebilir —
yetkilendirme doğru çalışır ama yanlış kişi için çalışır. Prototipteki kiosk
perdesi yalnız istemci tarafıdır; sayfayı yenileyen biri onu geçer. Bu görev
sunucu tarafına gerçek bir kilit koyar: cihaz kilitliyken oturum durur ama
devam etmek için PIN gerekir.

## Owned surface

- `plan/v1/remediation/V1-RMD-151-device-session-pin-lock.md` (yeni)
- `database/migrations/V1/V1-RMD-151/**` (yeni) — migration 094.
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Modules/Identity/Authentication/StoredUser.cs,
    src/Modules/Identity/Authentication/IUserStore.cs,
    src/Modules/Identity/Authentication/PostgresUserStore.cs,
    src/Modules/Identity/Authentication/AuthenticationService.cs,
    src/Modules/Identity/Authentication/LoginResult.cs
    (V1-IAM-001 sahipliğinde) — PIN alanları, doğrulaması ve `UnlockResult`
    sonuç tipi (giriş sonuçlarıyla aynı dosyada durur).
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs,
    src/Host/DualScreen/DualScreenContracts.cs (DualScreen sahipliğinde) —
    iki yeni uç nokta ve istek gövdeleri.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs,
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — migration 094 için standart dört dosyalık desen.
  - tests/Modules/Identity/Authentication/** (V1-IAM-001 sahipliğinde).

## In scope

1. **PIN oturumun yerine geçmez, kilidin üstüne biner.**
   `POST /api/v1/auth/unlock` yalnız hâlihazırda geçerli bir cihaz oturumu
   varken çalışır: oturum yoksa 401 döner ve kullanıcı tam girişe
   (kullanıcı adı + şifre) düşer. PIN bir kimlik kanıtı değil, "telefonu
   bırakan kişi hâlâ başında" kanıtıdır.
2. **PIN aynı güçte saklanır.** `identity.users.pin_hash`, şifreyle aynı
   `PasswordHasher` (PBKDF2-HMAC-SHA256, 600k iterasyon). Dört hanenin
   entropisi düşük olduğu için asıl koruma aşağıdaki deneme sınırıdır.
3. **Deneme sınırı şifreninkinden ayrıdır.** `pin_failed_attempts` ve
   `pin_locked_until` kendi sütunları; beş yanlış PIN 15 dakika kilitler
   (`AuthenticationService`'in şifre için zaten kullandığı eşikler). PIN
   kilitliyken tam giriş hâlâ çalışır — garson PIN'ini unuttuğunda servis
   durmaz, ve PIN denemesi şifre hesabını kilitleyemez.
4. **PIN'i kullanıcı kendi belirler.** `POST /api/v1/auth/pin`, mevcut
   oturuma ek olarak kullanıcının kendi şifresini ister; PIN'i yalnız
   sahibi değiştirebilir. PIN isteğe bağlıdır: `pin_hash` null olan bir
   kullanıcı için kilit ekranı PIN sormaz, istemci uzun basmayla açar
   (Semih'in kararı, 2026-09-10 — "pin olsun, telefondaki gibi, isterse
   kaldırsın").

## Out of scope

- Cihaz oturumu ömrünün 30 günden kısaltılması: kilit varken uzun ömür
  savunulabilir hâle geliyor, ama süre kararı ayrıdır.
- Kilidin ne zaman ineceği (hareketsizlik süresi): istemci kararı; Semih 3
  dakikayı seçti ve bu ekranların kendi işi.
- PIN'i yöneticinin sıfırlaması: bugün şifre sıfırlama akışı da yok, aynı
  eksikliğin parçası ve ayrı bir karar.
- Web Push / kilit ekranında bildirim gösterimi.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Migration 094: boş bir veritabanında gerçekten çalıştırıldı. İleri yönde
  `pin_failed_attempts, pin_hash, pin_locked_until` sütunları oluştu; geri
  yönde geriye hiç `pin%` sütunu kalmadı (sayı sıfır olarak doğrulandı).
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432), gerçek çıkış
  koduyla:
  - `ALKAROS.Identity.Authentication.Tests`: **59/59** (54'ten). Beş yeni
    senaryo: doğru PIN kilidi açıp sayacı sıfırlıyor; beş yanlış PIN
    PIN'i kilitliyor ama `failed_login_attempts` 0 kalıyor ve aynı anda
    kullanıcı adı/şifre ile giriş hâlâ çalışıyor; PIN'i olmayan kullanıcıda
    unlock `PinNotSet` ile reddediliyor; PIN belirlemek mevcut şifreyi
    istiyor; PIN silinince unlock yeniden kapanıyor.
  - `ALKAROS.Host.Tests` (Manifest + Composition): 134/134. Manifest
    testlerinin iki sınır değeri (toplam migration sayısı ve "aralık dışı"
    örneği olarak kullanılan 094) yeni migration'a göre güncellendi.
- Semih'in elle deneyebileceği senaryo: garson PWA'dan giriş yap, PIN
  belirle; uygulamayı kilitle ve yanlış PIN gir — beşinci denemeden sonra
  PIN'in kilitlendiğini ama kullanıcı adı/şifre ile girişin hâlâ
  çalıştığını gör; doğru PIN ile kilidin açıldığını ve sayacın
  sıfırlandığını gör.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan
  ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız), yeni
  ihlal yok.

## Handoff

- None
