# V1-RMD-770 - Silinmiş koda ait Done görevlere "yerine geçildi" notu

- Task ID: V1-RMD-770
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-739 Yüksek bulguları (V1-CUI-001, V1-FND-005, V1-FND-006, V1-FND-011, V1-FND-015, V1-FND-018): görevlerin teslimatı sonradan "sıfır tüketici, ölü kod" gerekçesiyle silindi (V1-RMD-134 commit 6b54f9ae: Idempotency, Transactions, TransactionOutboxIntegration ve Messaging içindeki Inbox; V1-RMD-135 commit 377458b7: Cashier TableShell), ama görev dosyaları kayıtsız şartsız "Done" kaldı ve kabul kanıtları silinmiş koda "VERIFIED" diyor. Okuyan kişi bu özelliklerin canlı olduğunu sanabilir.

Her görev dosyasına, silme commit'ini, gerekçeyi ve canlı karşılığı yazan bir not eklenir; görev metninin tarihsel kabul sözleşmesi değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-770-silinmis-kodun-gorev-notlari.md`
- `evidence/V1-RMD-770/**`
- Sınırlı ek: plan/v1/cashier-ui/V1-CUI-001-table-and-session-shell.md
- Sınırlı ek: plan/v1/foundation/V1-FND-005-transaction-execution-boundary.md
- Sınırlı ek: plan/v1/foundation/V1-FND-006-transaction-outbox-integration.md
- Sınırlı ek: plan/v1/foundation/V1-FND-011-transaction-outbox-atomicity.md
- Sınırlı ek: plan/v1/foundation/V1-FND-015-inbox-idempotency-contract.md
- Sınırlı ek: plan/v1/foundation/V1-FND-018-atomic-idempotency-execution.md

## In scope

- Altı görev md'sinin `Owned surface` bölümünün sonuna tek bir "V1-RMD-770 notu" maddesi.

## Out of scope

- Görevlerin Status değeri, kabul maddeleri, kanıt klasörleri ve silinen kodun geri getirilmesi.

## Dependencies

- V1-RMD-134
- V1-RMD-135

## Acceptance evidence

1. Plan audit ve consistency audit 0 hata, 0 uyarı; gerçek çıktıları kaydedilir.
2. Notlarda adı geçen silme commit'leri ve silinen yollar git geçmişinde doğrulanır.
3. Kapsam aracı InProgress baseline'a karşı exit 0 verir.

## Handoff

- None
