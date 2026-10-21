# V1-RMD-667 - PIN kilidi zinciri: sunucuda zorlama, yönetici açma, parola sayacı, PIN kuralları

- Task ID: V1-RMD-667
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulguları B001, B003, B004, B024 ve B032 kapanır. Beş yanlış PIN'den sonra sunucu garsonun cihaz oturumunu iptal eder; sayfa yenilemek ya da çerezle doğrudan API çağırmak kilidi atlatmaz. PIN kurma ve kaldırma ucunda yanlış mevcut parola, giriş ekranındaki gibi sayılır ve beş yanlışta hesap kilitlenir. Yönetici "kilidi aç" PIN kilidini ve PIN sayaçlarını da sıfırlar, kullanıcı arama sonucu PIN kilidini de "kilitli" gösterir. PIN dört ile on iki rakam arasındadır (yalnız 0-9), tuş takımının sınırıyla aynıdır; hepsi aynı rakam ya da ardışık dizi (1111, 1234, 4321) reddedilir. Giriş ve oturum yanıtı PIN'in kurulu olup olmadığını (`pinSet`) söyler, istemci kilit bayrağını buradan kurar. Garson uygulaması bağlantı yokken ya da sunucu hatasında "PIN hatalı" demez, girilen PIN'i silmez; sayfa yenilenince kilit ekranı veri yüklemesinden önce kurulur.

## Owned surface

- `plan/v1/remediation/V1-RMD-667-pin-lock-chain.md`
- `evidence/V1-RMD-667/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authentication/AuthenticationService.cs - yalnız PIN kurma ucunda parola sayacı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authentication/PostgresUserStore.cs - yalnız yönetici kilit açma
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authentication/IUserStore.cs - yalnız ForceUnlock açıklaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Endpoints.cs - yalnız unlock iptali, PIN kuralları, pinSet
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs - yalnız kullanıcı arama kilit durumu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/kiosk-lock.js - yalnız hata mesajları ve PIN uzunluğu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/auth.js - yalnız pinSet ile kilit bayrağı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/waiter-app.js - yalnız başlangıçta kilit sırası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/contracts.ts - yalnız ForceUnlock açıklaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authentication/AuthenticationServiceTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authentication/PostgresUserStoreTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-597-identity-quality-audit.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- B001: PIN kilitliyken unlock ucunda oturumun iptali ve çerezin silinmesi; istemcinin giriş ekranına dönmesi.
- B003: `SetPinAsync` içinde yanlış parolayı mevcut parola sayacıyla saymak ve kilitli hesapta reddetmek.
- B004: `ForceUnlockAsync` PIN sayaçlarını sıfırlar; kullanıcı arama `IsLocked` PIN kilidini de içerir.
- B024: PIN uzunluk ve zayıf PIN kuralı (sunucu), `pinSet` yanıtı, istemci hata ayrımı.
- B032: kilit ekranının veri yüklemesinden önce kurulması.

## Out of scope

- Doğru PIN ile oturum ömrünün mutlak üst sınırı (B019) ve Türkçe karakter/rakam dışı PIN'in eski kayıtları; yönetici ekranındaki PIN kilidi rozeti metni (V1-RMD-669). Gerçek tarayıcıda cihaz denemesi (doğrulanamadı: bu makinede tablet yok).

## Dependencies

- V1-RMD-597

## Acceptance evidence

1. Önce kırmızı testler (kilitli unlock sonrası çerez geçerli kalıyor; yanlış parolayla PIN kurma sayılmıyor; ForceUnlock PIN kilidini bırakıyor; arama PIN kilidini göstermiyor; zayıf ve uzun PIN kabul ediliyor), sonra yeşil; Yüksek bulgu için mutasyon kanıtı.
2. `evidence/V1-RMD-667/` altında çıktılar; Authentication, DualScreen host ve SecurityAdministration testleri yeşil.

## Handoff

- V1-RMD-598
