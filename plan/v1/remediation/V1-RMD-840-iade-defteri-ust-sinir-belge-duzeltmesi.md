# V1-RMD-840 - İade defteri üst sınırı belgesi: ödeme tutarı değil onaylanan tutar

- Task ID: V1-RMD-840
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: documentation
- Surface state: Existing

## Goal

Kasa incelemesi G11 grubu, kayıt F0347 (V0-DOM-003): `payment_reversals_guard` tetikleyicisi (182 numaralı migration) iade toplamını `payments.approved_amount` ile sınırlar; `docs/domain/refund-ledger.md` ise "payment amount" yazıyor. Kod daha sıkı ve doğru olan (`approved_amount` <= verilen tutar); belge koda uydurulur.

## Owned surface

- `evidence/V1-RMD-840/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/domain/refund-ledger.md (V0-DOM-003) — üst sınır sözcükleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Belgede iade üst sınırını geçen üç yer (`payment.amount`, "payment amount") `approved_amount` olarak düzeltilir; tetikleyicinin hatası ve kısıt adı ile eşleştiği `evidence/V1-RMD-840/RESULT.md`'de gösterilir.

## Out of scope

- Kod veya migration değişikliği.

## Dependencies

- V1-RMD-839

## Acceptance evidence

1. Belge diff'i ve tetikleyici metni karşılaştırması `evidence/V1-RMD-840/` altında.
2. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
