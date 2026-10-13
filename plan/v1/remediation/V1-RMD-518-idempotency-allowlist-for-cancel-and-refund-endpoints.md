# V1-RMD-518 - İptal ve iade uçlarının idempotency kaydı

- Task ID: V1-RMD-518
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

`tests/Architecture/ApiConventions` içindeki `EveryMutatingEndpointTakesAnIdempotencyKey` testi V1-RMD-506 ve V1-RMD-508 ile eklenen beş ucu (hesap iptali, iade isteği, iade onayı/reddi/tamamlama) idempotency anahtarı veya gerekçeli kayıt olmadan bulduğu için başarısızdır. Bu uçlar durum makinesiyle tekrar çağrıya karşı korumalıdır (ikinci çağrı 409 döner); gerekçe `idempotency_allowlist.json` içine görev numarasıyla kaydedilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-518-idempotency-allowlist-for-cancel-and-refund-endpoints.md`
- `evidence/V1-RMD-518/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Beş ucun kayıt gerekçesi: ikinci çağrı mevcut durum nedeniyle 409 döner (iptal edilmiş hesap, açık isteği olan ödeme, karara bağlanmış istek); kendi isteğini onaylama engeli. Mimari test geçer.

## Out of scope

- İstemci tarafı anahtar üretimi veya uçların davranış değişikliği.

## Dependencies

- V1-RMD-508

## Acceptance evidence

- Architecture ApiConventions testleri sıfır hata; çıktılar `evidence/V1-RMD-518/` altındadır.

## Handoff

- None
