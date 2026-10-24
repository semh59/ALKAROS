# V1-RMD-718 - Garson çevrimdışı kuyruğu ve hatalı tur ekranı

- Task ID: V1-RMD-718
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-602 bulguları B001 ve B028 kapanır. Çevrimdışı kuyruk 409'u HTTP durumuyla değil sunucunun hata koduyla ayırır: CONCURRENCY_CONFLICT gibi gerçekten geçici olanlar bekler, INSUFFICIENT_STOCK, PRODUCT_STOCK_NOT_CONFIGURED, TABLE_CHECK_ALREADY_OPEN, TABLE_LIMIT_REACHED, DUPLICATE_RESOURCE ve IDEMPOTENCY_KEY_REUSED hatalı tur listesine taşınır ve arkasındaki turları tıkamaz. Hatalı tur listesinde Sil onay ister, tur düzenlenip yeniden gönderilebilir; bekleyen QR siparişi reddi gerekçe seçtirir; küçük dokunma hedefleri büyür.

## Owned surface

- `plan/v1/remediation/V1-RMD-718-waiter-offline-queue-and-failed-rounds.md`
- `evidence/V1-RMD-718/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/** - yalnız kuyruk, hatalı tur ve bekleyen sipariş ekranları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/** - yalnız kuyruk ve hatalı tur testleri
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Kuyruk ayrımı, hatalı tur Sil/düzenle/yeniden gönder, QR reddi gerekçesi ve dokunma hedefleri. Sunucu hata kodları değişmez.

## Out of scope

- Sunucu tarafı idempotency değişiklikleri (V1-RMD-722). Rozet ve 'İptal iste' ifadesi doğrulanamadığı için bu görevde yalnız yeniden doğrulanır.

## Dependencies

- V1-RMD-602

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-718/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Semih senaryosu: stok yetersizliği yüzünden reddedilen bir tur, arkasındaki masaların turlarını tutmaz; hatalı tur listesinde görünür, düzeltilip yeniden gönderilir.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-603
