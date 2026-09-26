# V1-RMD-330 - Garson çevrimdışı kuyruğunun kararsız E2E testlerinin kök nedenini kapat

- Task ID: V1-RMD-330
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

CI'daki `tests/E2E/WaiterPwa/specs/06-offline-queue.spec.js` testleri ara sıra kırılıyor (2026-09-26 koşuları
36238168190 ve 36226232929). Kayıtlar iki gerçek ürün kusurunu gösteriyor:

- Service worker kaydı bir kez başarısız olursa (örneğin bağlantı, `sw.js` indirilirken koptuğunda) çevrimdışı mod
  kalıcı olarak "kurulamadı" durumunda kalıyor ve bir daha denenmiyor. Şerit o andan sonra bağlantı kopsa bile
  "Bağlantı yok" yerine "Çevrimdışı mod kurulamadı" yazıyor. Oysa kuyruk `localStorage` ile service worker
  olmadan da çalışıyor. Garson bağlantının koptuğunu göremiyor.
- Bir kuyruk gönderimi sürerken gelen tetik (bağlantı geri geldi, uygulamaya dönüldü) sessizce yok sayılıyor.
  Sürmekte olan gönderim geçici hatayla biterse tur, 15 saniyelik zamanlayıcıya kadar bekliyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-330-waiter-offline-flake-root-cause.md`
- `evidence/V1-RMD-330/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js ve src/Clients/WaiterPwa/wwwroot/js/ (V1-WTR-053) — kaydın
    yeniden denenmesi, şeridin öncelik sırası, gönderim sürerken gelen tetiğin korunması.
  - tests/Clients/StaticApps/ ve tests/E2E/WaiterPwa/specs/06-offline-queue.spec.js — testler.

## In scope

1. Service worker kaydı başarısız olursa bağlantı geri geldiğinde ve artan aralıklarla en çok 5 kez yeniden
   denenir. Başarılı kayıt "kurulamadı" durumunu kaldırır.
2. Şerit, bağlantı yokken service worker durumundan bağımsız olarak her zaman "Bağlantı yok — siparişler
   kuyrukta bekliyor" der. Çevrimdışı modun kapalı olma nedeni yalnız bağlantı varken gösterilir.
3. Gönderim sürerken gelen tetik kaydedilir. Sürmekte olan gönderim bitince kuyrukta tur kalmışsa gönderim hemen
   bir kez daha çalışır.
4. E2E testi, uygulamanın ikinci tetiği atladığını varsayan döngüyü bırakır; tek bir `visibilitychange` yeterlidir.

## Out of scope

- Diğer E2E dosyaları.

## Dependencies

- None

## Deliverables

- Kod ve testler.

## Acceptance evidence

- StaticApps vitest ve WaiterPwa E2E (yerelde tekrarlı koşu) yeşil; mutasyon kontrolü `evidence/V1-RMD-330/`
  altında.
- `task_scope_tool.py --task-id V1-RMD-330 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
