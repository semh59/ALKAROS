# V1-WTR-008 - Waiter PWA real API and reliable queue

- Task ID: V1-WTR-008
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

Waiter PWA içindeki hardcoded verileri ve mock token'ı kaldırmak; gerçek Host API'sine bağlamak; `flushOfflineQueue` içinde başarısız isteklerin kuyruktan silinmesini engelleyerek veri kaybını önlemek (yalnızca 2xx/authoritative onay sonrasında kuyruktan silme).

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/waiter-app.js`
- `tests/Clients/WaiterPwa/Frontend/**`
- `evidence/V1-WTR-008/**`

## Dependencies

- V1-WTR-007

## Acceptance evidence

- Waiter PWA gerçek Host API'den dinamik bölge, masa ve katalog verisi çeker.
- Çevrimdışı siparişler yalnız başarılı 2xx yanıt alındığında kuyruktan çıkarılır.
- Hata durumunda (401, 403, 409, 5xx) kullanıcıya net hata mesajı gösterilir ve sahte başarı bildirilmez.

## Handoff

- V1-RMD-038
