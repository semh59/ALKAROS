# V1-RMD-295 - Karar: çevrimdışı yetki mutabakatı uç noktasının istemcisi

- Task ID: V1-RMD-295
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`/api/v1/terminals/{id}/offline-reconciliation` (`OfflineReconciliationEndpoints.cs`) uç noktasını hiçbir istemci çağırmıyor. Ya kasa/garson bağlantı geri geldiğinde bunu çağırmalı (istemci eksik) ya da uç nokta artık gerekmiyor. Bu görev karar kaydıdır: `IOfflineGrantReconciler` akışını okuyup istemcinin çağırıp çağırmaması gerektiğini, gerekiyorsa hangi istemcinin ne zaman çağıracağını yazar; kod değişikliği ayrı görev olarak açılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-295-offline-reconciliation-client-decision.md`

## In scope

1. Kod okuması, karar kaydı ve gerekiyorsa uygulama görevinin açılması.

## Out of scope

- Uygulama.

## Dependencies

- V1-RMD-285

## Acceptance evidence

### Kod okuması

- `OfflineReconciliationEndpoints.cs`, `OfflineGrantReconciler.cs`, `OfflineAuthorityBudgetService.cs` ve `docs/domain/authorization-model.md` §5 okundu.
- Sunucu tarafı canlı ve gerçek: `POST /api/v1/auth/login` (`DualScreenApplication.Endpoints.cs`) HER girişte `offlineBudget` (budgetId, süre, `auto_within` yetkilerinin izin/limitleri) döndürüyor. `WaiterPwa`'nın `void-comp.js`'i `bills.void`/`bills.comp` için tam olarak bu "grant-class" akışı çağırıyor.
- Hiçbir istemci `offlineBudget` alanını okumuyor (`grep -rln "offlineBudget|budgetId" src/Clients` boş döndü). `void-comp.js` çevrimdışıyken düz `api()` çağrısı yapıyor, ağ hatasında genel "Sunucuya ulaşılamadı" yoluna düşüyor — çevrimdışı yetki bütçesi hiç devreye girmiyor.
- Uzlaştırmanın ÇIKTISI zaten tüketiliyor: `OfflineGrantReconciler` `Pending`/`Denied` kararları `identity.authorization_grants`'a yazıyor, PosTerminal'in `authorization-decisions` özelliği (`AuthorizationDecisionsWorkspace`) bu tabloyu okuyup yöneticiye onay/red ekranı sunuyor. Yani "yönetici inceler" ucu hazır, eksik olan yalnızca "istemci çevrimdışı yetkiyi kullanır ve bağlanınca bildirir" ucu.

### Karar

**Uç nokta kalmalı, kaldırılmamalı.** Tasarım gerçek, belgelenmiş (`docs/domain/authorization-model.md` §5) ve çıktısını tüketen bir ekran zaten üretimde. Ama bunu "istemci eksik, küçük bir çağrı ekle" olarak görmek yanlış olur: bugün HİÇBİR istemci çevrimdışı yetki bütçesini önbelleğe almıyor, yerel olarak bütçeye karşı denetim yapmıyor ya da yetkilendirilmiş eylemleri biriktirip bağlanınca göndermiyor — bu, tek bir çağrı değil, uçtan uca yeni bir istemci özelliği.

Bugünkü davranış (çevrimdışıyken `bills.void`/`bills.comp` başarısız olur, garson bağlanmayı beklemek zorunda kalır) güvenli bir geri düşüş: veri kaybı yok, sahte onay yok, yalnızca kullanılabilirlik eksik. Bu yüzden acil bir hata değil, ertelenebilir bir yetenek eksikliği.

**Hangi istemci, ne zaman:** WaiterPwa. Gerekçe: `bills.void`/`bills.comp` yalnız orada çağrılıyor (Cashier'ın kendi comp/void'u zaten yetkisi olan kullanıcı için anlık, çevrimdışı senaryosu farklı); WaiterPwa zaten kendi çevrimdışı kuyruk mimarisine sahip (`V1-RMD-149/171/185`, `alkaros_waiter_offline_queue`) ve `V1-RMD-285` (bu görevin bağımlılığı) az önce canlı bağlantı için "bağlantı geri geldi" anını net bir olay hâline getirdi — uzlaştırma çağrısı doğal olarak oraya eklenir. Zamanlama: `waiter-app.js`'in `window.addEventListener('online', ...)` işleyicisi (zaten `flushQueue()`'yu tetikliyor) ve/veya `V1-RMD-285`'in yeni `onreconnected` kancası.

**Yeni uygulama görevi açıldı: `V1-RMD-297`** (`offline-authorized-void-comp-reconciliation`), bu karara bağımlı. Öncelik notu: acil değil, güvenlik açısından hassas (yerel yetki denetimi) — Semih'in gözden geçirmesi önerilir, bu oturumun zorunlu sırasına eklenmedi.

## Handoff

- V1-RMD-297
