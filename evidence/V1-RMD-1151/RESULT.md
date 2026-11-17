# V1-RMD-1151 sonuç: ödeme öncesi "Hesap" ön fişi

Karar (Semih, 2026-11-18): ön fiş yapılır; mali olmayan bilgi fişi, ödenmiş hesabın orijinal/kopya akışından ayrı tür ve sayaç, masa akışı ve Bekleyen hesaplar'dan tek dokunuş.

## Yapılan
- Uç: `POST /api/v1/terminals/{terminalId}/orders/{orderId}/pre-receipt` (gövde `IdempotencyKey`), `src/Host/DualScreen/DualScreenApplication.Receipt.cs`. Migration yok.
- Hesap AÇMAZ (hesap açılırsa masaya ürün eklenemez, birleştirme kapanır): açık hesap varsa satırlar, düzeltmeler, ödenen hesaptan; yoksa sipariş satırlarından okunur. Ödenmiş hesap `BILL_NOT_UNPAID`, taslak/iptal/reddedilmiş/tamamlanmış sipariş `ORDER_NOT_OPEN`.
- Sayaç: `order.pre-receipt.printed` denetim kayıtları, sipariş satırı kilidi altında sayılır; aynı anahtar aynı numarayı alır. Ödenmiş hesabın `receipt_first_printed_at` alanına dokunulmaz (test: iki ön fişten sonra ödenmiş hesabın ilk basımı hâlâ orijinal).
- PosTerminal: Bekleyen hesaplar satırında ve masa akışında "Hesap fişi" (48 px; yazdırma yazısı 14 px). Fiş yalnız yazdırma süresince sayfada durur (masa QR yazdırmasını bozmaz). Fişte "Bu belge mali fiş değildir.", ön fiş numarası, satırlar, düzeltmeler, toplam, ödenen/kalan vardır; KDV dökümü, fiş sıra no, mali değer iddiası yoktur.
- Mali müşavirle teyit notu: `docs/domain/bill-pre-receipt.md` (ibare ve e-Adisyon, VUK 509 IV.12 uygulanabilirliği dış bağımlılık).

## Kapsam kararları (kayıt)
- Yetki: görev dosyası Goal'unda `payments.take` yazıyordu; uç `bills.split` ister, çünkü masa akışı ve Bekleyen hesaplar "Öde"yi bu yetkiyle gösterir (qa-frontend bulgusu). Sapma docs'ta da yazılı.
- Satış ekranının "Tahsil et" düğmesi ve hesap bölme sayfası kapsam dışı (görev Out of scope).
- Numara siparişe özgüdür (birleşik hesapta her sipariş kendi sayacı).

## Kanıt
- Kırmızı: `red-host.log` (7 test, uç yokken 7/7 başarısız). İstemci testleri uygulamayla birlikte yazıldı; gücü mutasyonla gösterildi.
- Yeşil: `green-host.log` (BillReceiptHttpTests 42/42), `pos-full.log` (127 dosya, 1536 test), `api-conventions.log` (17/17), `client-build-gate.log` (lint, tip, vite build exit 0), `plan-audit.log` (0 hata, 0 uyarı), `consistency-audit.log` (clean).
- Mutasyon: `mutations.txt`, 34 mutant, 34 öldürüldü (13 sunucu, 21 istemci). Her mutantta kopyala-geri al-cmp yapıldı. `if (false)` biçimli iki ilk deneme derleme uyarısı hatasıyla bozuldu, başka kalıpla yeniden denendi; `C1` kalıbı kod değişince eşleşmedi, `C1b` ile yeniden denendi (ilk denemelerin kaydı silindi).
- qa: `REVIEWS.md` (qa-backend, qa-frontend; bulguların hepsi düzeltildi veya gerekçeyle bırakıldı).

## Açık risk
- `ErrorMessageLanguageTests.NoResponseMessageOfTheTenderCashAndRefundEndpointsIsEnglish` (PaymentTender projesi) bu görevden önce de kırmızı: `ApiErrorCatalog.cs` içindeki "Bir gerekçe seçin; "Diğer" için not yazın ..." iletisindeki "not" (Türkçe: not) sözcüğünü İngilizce sayıyor. Bu görevin dosyalarında değil, `host-paymenttender-full.log` içinde görülür; ayrı görev gerekir.
- Yazdırma, tarayıcı yazdırma penceresiyle yapılır; yazıcıya gerçekten gittiği doğrulanamaz (V1-RMD-887 ile aynı sınır).
