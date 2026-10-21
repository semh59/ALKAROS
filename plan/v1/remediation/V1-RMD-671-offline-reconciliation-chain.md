# V1-RMD-671 - Çevrimdışı yetki zinciri: kayıp, sayım yarışı, parti sınırı, hata gövdesi ve cihaz verisi

- Task ID: V1-RMD-671
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulguları B007 (Yüksek), B011, B012, B014, B023 ve B030 kapanır. Garsonun çevrimdışı verdiği iptal/ikram eylemleri artık geçici hatada (bağlantı, 408, 423, 429, 5xx) cihazda kalır ve sonra yeniden gönderilir; yalnız sunucunun kalıcı reddettiği eylem silinir ve sunucunun Türkçe nedeni gösterilir. Uzlaştırma hata gövdesi öteki uçlarla aynı `error` zarfında gelir, ekranda Türkçe mesaj görünür. Aynı bütçenin sayımı kilit altında yapılır, parti içi eylemler iki kez sayılmaz ve yeniden girişte bütçe sıfırlanmaz (daha önce harcanan sayı düşülür). Uzlaştırma partisi en çok 50 eylem alır, hepsi yazmadan önce doğrulanır; ileri saatli ya da bütçeden önceki saatli tek eylem partiyi düşürmez, o eylem reddedilir. Sipariş kuyruğu 401/403/408/409/423 yanıtında turu kalıcı hatalı listesine atmaz. Çıkış ağ hatasında sunucu oturumunu açık bırakıp sayfayı yenilemez; kullanıcı değişince önceki kullanıcının taslakları, çevrimdışı eylemleri ve hatalı sipariş listesi silinir; iptal talebinin anahtarı yenilemede kaybolmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-671-offline-reconciliation-chain.md`
- `evidence/V1-RMD-671/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs - yalnız parti sınırı, kilit ve hata zarfı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Offline/OfflineGrantReconciler.cs - yalnız doğrulama, sayım ve zaman kuralları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Offline/IOfflineReplayLedger.cs - yalnız bütçe kilidi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Offline/PostgresOfflineReplayLedger.cs - yalnız bütçe kilidi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Offline/PostgresOfflineAuthorityBudgetRepository.cs - yalnız harcanan sayının devri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/offline-queue.js - yalnız geçici hata kuralı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/state.js - yalnız cihaz verisini silme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/sheets/void-comp.js - yalnız anahtarın saklanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/waiter-app.js - yalnız çıkış ve kullanıcı değişimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OfflineReconciliation/OfflineReconciliationHttpTests.cs - yalnız yeni ve uyarlanan testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Offline/OfflineGrantReconcilerTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Offline/PostgresOfflineAuthorityBudgetRepositoryTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/offline-queue.test.js - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-669-decision-and-staff-screens.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- B007: uzlaştırma yanıtında geçici/kalıcı ayrımı; geçici hatada eylemler kalır.
- B011: bütçe başına danışman kilidi, parti içi çift sayımın kaldırılması, yeniden girişte harcananın devri.
- B012: 50 eylem sınırı, önce doğrulama, ileri/geri saatli eylemin tek başına reddi.
- B014: `error` zarfı.
- B023, B030: istemci kuralları.

## Out of scope

- Uzlaştırma ucuna hız sınırı (parti sınırı yeterli; yeniden denemeler yinelenen anahtarla güvenlidir). Bütçenin veritabanı kısıtları (V1-RMD-673). Başka kullanıcının gönderilmemiş sipariş turunun yeni kullanıcı adına gönderilmesi (tur kuyruğu silinmez; ürün kararı gerekir). Tarayıcıda elle deneme (doğrulanamadı).

## Dependencies

- V1-RMD-669

## Acceptance evidence

1. Her kural için test; B007, B011 ve B012 için mutasyon kanıtı.
2. `evidence/V1-RMD-671/` altında çıktılar; Offline, OfflineReconciliation ve StaticApps testleri yeşil.

## Handoff

- V1-RMD-672
