# V1-RMD-933 - Ödeme ekranı hata sözlüğü indirim, bahşiş, iade ve başka kasa kodlarını da karşılasın

- Task ID: V1-RMD-933
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-925 incelemesinden çıkan boşluk: ödeme ekranının hata sözlüğünde DISCOUNT_VALUE_INVALID, DISCOUNT_TYPE_INVALID, TIP_AMOUNT_INVALID, NOTES_TOO_LONG, ADJUSTMENT_NOT_UNDOABLE, IDEMPOTENCY_KEY_REQUIRED, ALLOWANCE_INVALID, REFUND_TEXT_INVALID, REFUND_INPUT_INVALID ve BILL_OTHER_TERMINAL için Türkçe karşılık yok; ekran genel bir cümleye düşüyor. Kilit testi de BillingSplitStore.cs'i ve çok satırlı hata biçimlerini taramıyor. Karar: sabit cümleli kodlar sözlüğe sunucuyla aynı cümleyle girer, sayı içeren cümleler sunucu metnini geçirme listesine girer; kilit testi bu kaynakları ve biçimleri tarar. Ayrıca V1-RMD-932 incelemesinden çıkan test sıkılaştırması (reddedilen kart ödemesinden sonra yöntem seçimi sıfırlanır) bu görevde sonlanır.

## Owned surface

- `evidence/V1-RMD-933/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/error-dictionary-lock.test.js
  - tests/Clients/StaticApps/split-payment-display-binding.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı kilit testi, sözlük düzeltmesi, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Sunucu cümlelerinin değiştirilmesi

## Dependencies

- V1-RMD-932

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
