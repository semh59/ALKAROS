# V1-RMD-969 - Fiş orijinal sıfırlama ucu idempotency allowlist'inde kayıtlı olsun

- Task ID: V1-RMD-969
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-901 POST .../receipt/original-reset ucunu ekledi; ApiConventions testi EveryMutatingEndpointTakesAnIdempotencyKey bu ucun anahtarsız olduğu için kırmızı. Uç doğası gereği tekrar güvenli: sıfırlama basım izini siler, ikinci istek sıfırlanacak orijinal bulamayıp 409 RECEIPT_NOT_PRINTED döner ve ikinci denetim kaydı oluşmaz (PostgresBillReceiptPrintRepository.ResetOriginalAsync, BillReceiptHttpTests). Karar: uç, gerekçesiyle allowlist'e eklenir.

## Owned surface

- `evidence/V1-RMD-969/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Architecture/ApiConventions/idempotency_allowlist.json
  - tests/Architecture/ApiConventions/MutatingEndpointIdempotencyTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-968

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
