# V1-RMD-311 - Geri ödeme niyetinde eşzamanlı aynı-anahtar yarışını kapat

- Task ID: V1-RMD-311
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Faz 3 sonrası CI'ın ilk tam çalıştırmasında (`36219456105`)
`PostgresRefundIntentRepositoryTests.ConcurrentDuplicateSubmitsProduceExactlyOneRealIntent` `23505`
(`uq_refund_intents_idempotency_key`) ile kırmızıya döndü. `PostgresRefundIntentRepository.CreateAsync`
idempotency anahtarını tahsis kilidinden ÖNCE okuyor. Aynı anahtarla gelen iki istek de "yok" görüyor ve kilidi
sırayla alıyor; ikincisi yeniden bakmadan ekleme yapıp benzersizlik ihlaline düşüyor. Mevcut test yarışa bağlı
olduğu için bu hatayı yalnız ara sıra yakalıyor. Semih: "en küçük hata bile kritik".

## Owned surface

- `plan/v1/remediation/V1-RMD-311-refund-intent-idempotency-race.md`
- `evidence/V1-RMD-311/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Payments/Allocations/RefundIntents/PostgresRefundIntentRepository.cs (V13-ALC-003) — kilit sonrası
    yeniden okuma ve çakışmada mevcut kaydı döndürme.
  - tests/Modules/Payments/Allocations/RefundIntents/ — deterministik yarış testi.

## In scope

1. Kilit alındıktan sonra idempotency anahtarı yeniden okunur; varsa mevcut kayıt döner.
2. Ekleme, anahtar çakışmasında hata atmaz (`ON CONFLICT ... DO NOTHING`). Yazılmayan ekleme mevcut kaydı döndürür.
   Bu, farklı tahsisler için aynı anahtarla gelen eşzamanlı istekleri de kapsar.
3. Deterministik test: test tahsis kilidini tutar, iki istek kilitte bekler, kilit bırakılınca tek kayıt oluşur ve
   ikisi de aynı kaydı döndürür.

## Out of scope

- Geri ödeme sonuçlandırma (V13-ALC-004).

## Dependencies

- None

## Deliverables

- Düzeltme ve deterministik test.

## Acceptance evidence

- RefundIntents test projesi yeşil; mutasyon kontrolü `evidence/V1-RMD-311/` altında.
- `task_scope_tool.py --task-id V1-RMD-311 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
