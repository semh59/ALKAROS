# V1-RMD-850 - Garson çevrimdışı iptal/ikram: aynı anahtar bir kez kuyruğa girer, giriş ve bağlantı davranış testleri

- Task ID: V1-RMD-850
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (V1-RMD-849 incelemesi, gerçek kusur): çevrimdışı iptal/ikram onaylandıktan sonra kalem ekranda kalır; sayfa aynı kalem için aynı işlem anahtarını yeniden verir (`pendingVoidKeys`/`pendingCompKeys`), `queueOfflineAuthorizedAction` ise aynı `idempotencyKey` ile ikinci kaydı kuyruğa ekler ve yerel yetki bütçesini ikinci kez harcar (harness çıktısı `["k-void","k-comp","k-void"]`). Aynı anahtar kuyrukta bir kez bulunur, bütçe bir kez harcanır. Ayrıca `waiter-app.js` için kaynak-metni testiyle korunan iki kablolama (girişte yetki bütçesinin saklanması, `online`/görünür olma/canlı bağlantı dönüşünde uzlaştırma) gerçek davranış testine çevrilir ve kaynak-metni testi silinir.

## Owned surface

- `evidence/V1-RMD-850/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/WaiterPwa/wwwroot/js/offline-queue.js — `queueOfflineAuthorizedAction` tekrar denetimi.
  - tests/Clients/WaiterPwa/Frontend/test_offline_authorized_void_comp.py — tekrar testi, kaynak-metni testinin kaldırılması.
  - tests/Clients/StaticApps/waiter-app.test.js — giriş ve bağlantı dönüşü davranış testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı: aynı anahtarla ikinci onay kuyruğu 3 kayda çıkarıyor; giriş yanıtındaki bütçe saklanmıyorsa ve `online` olayı uzlaştırma isteği atmıyorsa testler düşer (kırmızı log).
2. Düzeltme: kuyrukta aynı `idempotencyKey` varsa ne kayıt eklenir ne bütçe sayacı artar.
3. Mutasyon: tekrar denetimi kaldırılırsa, giriş bütçe saklaması kaldırılırsa, `online` uzlaştırma çağrısı kaldırılırsa testler düşer (cp+cmp).

## Out of scope

- Garson PWA'nın diğer davranışları, sunucu uzlaştırma ucu.

## Dependencies

- None

## Acceptance evidence

1. Python (WaiterPwa Frontend) ve StaticApps vitest koşuları exit 0; çıktı `evidence/V1-RMD-850/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Elle deneme: tarayıcıyı çevrimdışı yapıp aynı kalemi iki kez iptal et; çevrimiçi olunca tek istek gider.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
