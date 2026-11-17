# V1-RMD-1144 - Kasa kapanışında "Yönetici burada onaylasın": fark toleransı aşılınca yönetici adı ve şifre

- Task ID: V1-RMD-1144
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 5): kapanışta fark toleransı aşılınca ekran yalnız bir açıklama kutusu gösteriyor, kasiyer onay veremiyor. V1-RMD-1143 ucu kullanılır; V1-RMD-1129 indirim onayı kalıbı kapanışa uygulanır.

1. Fark toleransı aşıldığında ("Fark tolerans sınırını aşıyor") mevcut açıklama kutusunun yanında "Yönetici kullanıcı adı" ve "Yönetici şifresi" alanları çıkar (görünür etiket, parola türü, tarayıcı otomatik doldurmaya kapalı, dokunma hedefi 48 px) ve "Kasayı Kapat" yönetici onayıyla kapanışı gönderir (`IsSupervisorOverride`, `ManagerUsername`, `ManagerPassword`, açıklama). Alanlar boşsa istemci Türkçe cümleyle durdurur ve hiçbir istek göndermez.
2. Hatalar sabit Türkçe cümledir (sözlükten, ham sunucu metni ya da kod görünmez): kimlik hatalı, yönetici kilitli, çok fazla deneme, kendi onayı, yönetici yetkisiz; cümleler V1-RMD-1129 ve V1-RMD-1142 ile aynıdır. Şifre yalnız alanda yaşar: durum nesnesine, depolamaya, adrese ve günlüğe yazılmaz; her denemeden sonra alan boşaltılır.
3. Başarıda "Kasa Kapatıldı" özeti gelir (mevcut kapanış sonrası akış). Tolerans aşılmadıysa ekran ve istek değişmez.

## Owned surface

- `evidence/V1-RMD-1144/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/cash-session-close-manager-approval.test.js
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css
  - tests/Clients/StaticApps/cash-session-close.test.js
  - tests/Clients/StaticApps/cash-session-close-recount.test.js
  - tests/Clients/StaticApps/cash-session-text-size.test.js
  - tests/Clients/StaticApps/cashier-touch-targets.test.js
  - tests/Clients/StaticApps/error-dictionary-lock.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu sözleşmesi (V1-RMD-1143); PIN ile onay; sayım ekranı; yönetim ekranı

## Dependencies

- V1-RMD-1143

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None
