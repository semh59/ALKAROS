# V1-RMD-840 sonuç

`docs/domain/refund-ledger.md` iade üst sınırını "ödeme tutarı" (`payment.amount`) olarak yazıyordu; tetikleyici (`payments.payment_reversals_guard`, migration 182) toplamı `payments.approved_amount` ile sınırlıyor (`already + NEW.amount > approved` ise `check_violation`, "refund exceeds the approved amount of payment"). Onaylanan tutar verilen tutardan büyük olamaz (`ck_payments_approved_amount_not_exceeding_tendered`, migration 121), yani kod daha sıkı; belgenin üç yeri (satır 34, 39, 73) koda uyduruldu. Fark `doc.diff` içinde. Kod ve migration değişmedi.

## İncelemeler (qa-dokuman-bagimlilik, qa-backend)
İkisi de bulgu bulmadı; üç değişiklik tetikleyici, depo (`PostgresPaymentReversalRepository`), `Payment.cs` ve `PaymentRefunds.cs` ile birebir uyuşuyor. Kapsam dışı, kabul edilen: belge satır 17 sütunu `payment_reversals.id` diyor, gerçek ad `reversal_id`; tarihsel kayıtlar (`evidence/V0-DOM-003/decision-record.md`, V1-RMD-507 planı) eski sözcüğü taşıyor ve değiştirilmedi.
