# V1-RMD-757 bağımsız incelemeler

Kod incelemesi ve test/kanıt incelemesi ayrı ajanlarla, HEAD 27d0dc52 üzerinde yapıldı. İkisinde de engelleyici bulgu yok.

## Kod
- SQL ve şema doğru (`ck_refund_requests_status` 'Completed' kabul eder; durum `MarkCompletedAsync` ve `MarkApprovedAsync` ile yazılır).
- `refundedQuantity` anlamı değişmedi; kalan-adet hesabı (RefundPanel, split-payment.js) bozulmadı.
- Fişteki para iadesi (`payments[].refundedAmount`) yalnız tamamlanmış iadede yazılan reversal toplamıdır; adet ve tutar aynı kümeyi (Completed) yansıtır.
- `BillReceiptItemV1` tek yerde üretilir; JSON geriye uyumludur. Ölü kod yok.
- Düşük: kısmi tutarlı reversal ileride eklenirse adet ile tutar ayrışabilir; bugün böyle yol yok. Derlenmiş `PosTerminal/dist` kopyası gitignore'dadır.

## Test ve kanıt
- Kırmızı koşu doğru nedenle düştü; 3 mutant öldü; dört kaynak dosya temiz ve hash'ler `green.json` ile aynı.
- Düşük: Rejected durumu yeni Host testinde yok; `EachItemCarriesItsIdAndTheUnitsAlreadyAskedToBeRefundedButNotTheRejectedOnes` Rejected'ın `refundedQuantity`'ye girmediğini kanıtlar ve yeni alan `status = 'Completed'` ile saydığı için Rejected yapısal olarak dışarıdadır.
- `split-payment-item-refund.test.js` yardımcısı o testi zayıflatmaz (kalan adedi sınar, fiş satırını değil).
