# V1-RMD-859 - Kasa belge sapmaları kodla hizalanır: kapanış farkı, iade defteri, tahsilat değişmezleri, değer kataloğu, tablo adları, projeksiyon yazarları

- Task ID: V1-RMD-859
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (belge-kod sapmaları, kod esas): (1) cash-session-design.md ClosingDifference satırını 'fark sıfır değilse' diye yazmıyor, kod yalnız fark sıfırdan farklıysa yazıyor; (2) refund-ledger.md `payment_reversals.id` diyor, kolon `reversal_id`; mali belge bağlantı alanı belgede sayılıyor ama tabloda yok (mali cihaz entegrasyonu kurulmadı); (3) payment-allocation-integrity.md 'her tahsilat türü fazla ödeme alabilir' diyor, yalnız nakit alabilir (kart ve havale aşımı reddedilir), 'kalan = fatura.payable - dağılımlar' diyor, kod indirim ve bahşiş sonrası düzeltilmiş tutarı kullanır, `payment.amount = sum + change` diyor, kısıt `change_amount = tendered_amount - approved_amount`; (4) canonical-value-catalog.md `payments.payment_method` sütunu yok, yöntem türetilir ve Eft eksik; (5) migration-dependency-graph.md PhaseBMax 201 diyor (219), `refund_ledger_entries` aslında `payment_reversals`, `payment_providers` yok; (6) reporting-metrics.md olmayan tablo adları (`bill_closures`, `inventory_movements`, `reconciliation_cases`, `backup_jobs/artifacts`, `invoices` şemasız, `meal_card_settlements`); (7) projection-ownership.md yedi olay adı kodda yok. Token/QNB gerekçe notu (V13-GOV-007, token-integration-path-decision.md) bir karar belgesi olduğu için bu görevde değiştirilmez, ürün sahibine bildirilir.

## Owned surface

- `evidence/V1-RMD-859/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/domain/cash-session-design.md — ClosingDifference yalnız fark sıfır değilse.
  - docs/domain/refund-ledger.md — reversal_id ve mali bağlantı alanı notu.
  - docs/domain/payment-allocation-integrity.md — fazla ödeme yalnız nakit, düzeltilmiş kalan tutar, change_amount kısıtı.
  - docs/data/canonical-value-catalog.md — ödeme yöntemi türetilir.
  - docs/data/migration-dependency-graph.md — PhaseBMax ve tablo adları.
  - docs/domain/reporting-metrics.md — gerçek tablo adları.
  - docs/data/projection-ownership.md — kodda olmayan olay adları notu.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her düzeltme önce kodla doğrulanır (migration, enum, işleyici); belge koda uydurulur, kod değişmez.
2. Plan ve tutarlılık denetimleri sıfır hata sıfır uyarı.

## Out of scope

- Kod ve migration; Token/QNB karar belgeleri (ürün sahibinin kararı); belge dışı eski görev notları.

## Dependencies

- None

## Acceptance evidence

1. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
2. Her düzeltmenin kod kanıtı `evidence/V1-RMD-859/RESULT.md` içinde dosya:satır olarak listeli.
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
