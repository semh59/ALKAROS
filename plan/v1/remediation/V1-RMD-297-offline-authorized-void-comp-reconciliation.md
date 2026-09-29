# V1-RMD-297 - Garson çevrimdışı iptal/ikram bütçesini kullanır, bağlanınca uzlaştırır

- Task ID: V1-RMD-297
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

`V1-RMD-295`'in karar kaydı: sunucu her girişte `offlineBudget` (`bills.void`/`bills.comp` gibi `auto_within` yetkilerinin çevrimdışı limitleri) döndürüyor (`DualScreenApplication.Endpoints.cs`), ama hiçbir istemci bunu okumuyor; `void-comp.js` çevrimdışıyken düz ağ hatası veriyor, garson bağlanmayı beklemek zorunda kalıyor. Bu görev WaiterPwa'ya çevrimdışı yetki bütçesini önbelleğe alma, bütçe dahilindeyken iptal/ikramı yerel olarak izin verip kuyruğa alma, ve bağlantı geri gelince `/api/v1/terminals/{id}/offline-reconciliation`'ı çağırıp sonucu garsona Türkçe özetleme yeteneğini ekler. Bütçe dışına çıkan bir istek çevrimdışı reddedilir ("bağlanınca tekrar deneyin"), asla sahte onaylanmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-297-offline-authorized-void-comp-reconciliation.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/waiter-app.js
  (yalnız giriş yanıtından bütçe önbelleğe alma ve bağlanınca uzlaştırma çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/sheets/void-comp.js
  (yalnız çevrimdışı dal)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/offline-queue.js
  (yalnız çevrimdışı yetkilendirilmiş eylem kaydı ve uzlaştırma tetikleyicisi)
- Yeni dosya: tests/E2E/WaiterPwa/specs/10-offline-authorized-void-comp.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/10-offline-authorized-void-comp.spec.js (yeni),
  tests/E2E/WaiterPwa/global-setup.js ve tests/E2E/WaiterPwa/lib/testHelpers.js — yalnız çevrimdışı senaryonun
  kurulumu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/auth.js, src/Clients/WaiterPwa/wwwroot/js/api.js
  ve src/Clients/WaiterPwa/wwwroot/js/state.js — yalnız giriş yanıtındaki bütçenin saklanması ve çevrimdışı
  denetimi
- `tests/Clients/WaiterPwa/Frontend/test_offline_authorized_void_comp.py` (yeni) — istemcinin statik testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Endpoints.cs — yalnız giriş
  yanıtındaki `offlineBudget` nesnesine bütçenin rol kodunun (`roleCode`) eklenmesi: uzlaştırma ucu her eylemde
  `RequesterRoleCode` ister ve istemci bu kodu başka bir yanıttan öğrenemez; bütçe hizmeti ve uzlaştırıcı değişmez
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OfflineReconciliation/OfflineReconciliationHttpTests.cs
  — yalnız giriş yanıtındaki `roleCode` alanının testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/lib/seed.js — yalnız E2E garson rolüne `auto_within`
  iptal/ikram politikası (varsayılan kurulumda çevrimdışı bütçe satırı yoktur)

## In scope

1. Giriş yanıtındaki `offlineBudget`'ın önbelleğe alınması (bütçe kimliği, kalemler, geçerlilik süresi).
2. Çevrimdışıyken `bills.void`/`bills.comp` için yerel bütçe denetimi: dahilindeyse yerel olarak izin verip idempotency anahtarıyla kuyruğa al, dışındaysa reddet ve "bağlanınca tekrar deneyin" mesajı göster.
3. Bağlantı geri gelince (`online` olayı ya da `V1-RMD-285`'in `onreconnected` kancası) kuyruklanan eylemleri `/offline-reconciliation`'a gönderme, sonuçtaki `Brief` metnini garsona gösterme.
4. Süresi dolmuş ya da tükenmiş bütçe: çevrimdışı her eylem doğrudan reddedilir, sahte onay asla verilmez.
5. Testler: vitest/statik istemci testi ve gerçek Host + Chromium E2E.

## Out of scope

- Sunucu tarafı değişikliği (`OfflineGrantReconciler`, `OfflineAuthorityBudgetService` zaten gerçek ve test edilmiş).
- Cashier/PosTerminal'in kendi comp/void akışı (yetkili kullanıcı için zaten anlık, çevrimdışı senaryosu bu görevin kapsamında değil).
- Yönetici inceleme ekranı (`AuthorizationDecisionsWorkspace` zaten var, `V1-RMD-295`'te doğrulandı).

## Dependencies

- V1-RMD-295
- V1-RMD-285

## Acceptance evidence

- Vitest ya da statik istemci testi: bütçe dahilinde çevrimdışı iptal/ikram yerel olarak kuyruğa alınır; bütçe dışında reddedilir; bağlanınca uzlaştırma çağrısı gider ve `Brief` gösterilir.
- WaiterPwa E2E (gerçek Host + Chromium): çevrimdışıyken bütçe dahilinde bir ikram kuyruğa alınır, ekranda "bağlanınca uzlaştırılacak" durumu görünür; bağlantı gelince gerçek `/offline-reconciliation` çağrısı gider ve sunucunun döndürdüğü özet ekranda görünür; bütçe dışı bir istek çevrimdışıyken reddedilir (kuyruğa girmez).
- Mutasyon kontrolü: bütçe denetimi ya da uzlaştırma çağrısı geri alınınca yukarıdaki testler kırılmalı.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.

## Handoff

- None
