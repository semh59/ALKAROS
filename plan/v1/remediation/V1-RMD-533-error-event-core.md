# V1-RMD-533 - Hata olayı çekirdeği

- Task ID: V1-RMD-533
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: New

## Source basis

- PO:2026-10-15

## Goal

Her hata, katman (veritabanı, api, arka plan, yazıcı, ödeme, mutfak, entegrasyon, tarayıcı, güvenlik, yedek) ve şiddetle tek bir hata olayı olarak kaydedilir; aynı hata parmak izi ile tekilleştirilir, sayaç artar, olay kaybolmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-533-error-event-core.md`
- `evidence/V1-RMD-533/**`
- `src/Modules/Observability/ErrorEvents/**`
- `tests/Modules/Observability/ErrorEvents/**`
- `database/migrations/V1/V1-RMD-533/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Hata olayı modeli, parmak izi (sayılar, kimlikler ve değişken metin normalize edilir), `observability.error_events` ve `observability.error_occurrences` tabloları.
- Tekilleştirme veritabanında (süreç belleğinde değil): ilk görülme, son görülme, sayaç, çözüldü işareti.
- Önce Postgres'e yazar; kuyruk dolsa da olay düşmez. Yazma hatası ana işlemi bozmaz.

## Out of scope

- Telegram gönderimi; dedektörler; ekran.

## Dependencies

- None

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-533/` altındadır.

## Handoff

- None
