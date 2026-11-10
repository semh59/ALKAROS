# V1-RMD-908 sonucu

Orijinal fişi basan kullanıcı o hakkı kendisi sıfırlayamaz (403 RECEIPT_RESET_SELF, başka yönetici gerekir); bir hesabın orijinal hakkı en fazla 2 kez sıfırlanır (409 RECEIPT_RESET_LIMIT, sayı bill.receipt.original.reset olaylarından sayılır, hak yanık kalır). Kasiyer ekranı iki hatayı Türkçe gösterir.

## Kanıt
- Kırmızı (red-1.log): 2 test; yeşil: BillReceiptHttpTests 26/26 (green-1.log), istemci split-payment-receipt-reset 9/9 (vitest-1.log).
- Mutantlar (mutations.txt): 4 öldü.

## Ürün varsayımı
- Sınır 2 bu görevin varsayımıdır (ayar değil, sabit); farklı sayı istenirse tek sabit.
- Kasada tek yönetici varsa kendi bastığı fişin hakkını sıfırlayamaz; başka bir yönetici gerekir.

## Notlar
- Basanı bilinmeyen eski kayıtta (printed_by null) kendi kendine sıfırlama engellenmez (kayıt eskiden kalma; kabul).
- Sayaç denetim olaylarından sayılır; denetim bölümleri 10 yıl sonra elle atılır, hesabın fiş akışından uzun (kabul).
