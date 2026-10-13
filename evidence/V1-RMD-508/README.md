# V1-RMD-508 kanıtı

Sonuç: ödenmiş hesap önce iade edilir; iade isteği, onay (veya reddi), elle iade sonucu ve tüm ödemeler iade edilince hesabın kapanması gerçek Host + Postgres testleriyle çalışıyor. Ekranı yok (V1-RMD-509).

## Akış
- `POST .../billing/bills/{billId}/refund-requests` (`payments.refund.request`, kasiyer ve üstü): ödeme + gerekçe; tam tutar. Nakit satışı olan ödeme `Cash`, diğerleri `Manual`; müşteri hesabına yazılan ödeme 409 (`ACCOUNT_REFUND_UNSUPPORTED`). Ödeme başına tek açık istek.
- `POST .../billing/refund-requests/{id}/approve` (`payments.refund.approve`, süpervizör ve müdür; isteyen kişi onaylayamaz → 403 `SELF_DECISION`):
  - Nakit: isteyen kasiyerin açık oturumuna `Refund` (çıkış) satırı + iade defteri satırı + ödeme `Refunded`; açık oturum yoksa 409 `NO_OPEN_CASH_SESSION`, hiçbir şey değişmez.
  - Diğer yöntem: `AwaitingManualRefund`; para banka/POS panelinden elle iade edilir.
- `POST .../refund-requests/{id}/complete`: fiş numarası zorunlu; iade defteri satırı + ödeme `Refunded`.
- `POST .../refund-requests/{id}/reject`: gerekçe zorunlu.
- Hesabın hiçbir ödemesi para tutmuyorsa (hepsi iade) hesap `Cancelled` olur; kalan ödeme varsa hesap açık kalır. İptal ucu iade edilmiş ödemenin ayırmasını para saymaz.
- Denetim olayları: `payment.refunded` (aktör, gerekçe, yöntem, fiş no, hesap iptal edildi mi), `refund.rejected`.
- Migration 183 (`payments.refund_requests`), 184 (izinler: kasiyer isteme, süpervizör/müdür onaylama).

## Kanıt
- `test-runs.log`: Billing Host 46/46 (13 yeni iade testi), yetki 223/223, manifest + migration zinciri 25/25.
- `mutations.log`: 16 mutant, hepsi KILLED (ilk turda sağ kalan dört mutant için test eklendi).

## Açık risk / kapsam dışı
- Kısmi iade yok; yalnız ödemenin tamamı iade edilir.
- Müşteri hesabına (cari) yazılan ödemenin iadesi yok (409); cari borç düzeltmesi elle yapılmalı.
- İade sonrası sipariş `Completed` kalır; ürün bazlı satış raporları iade edilmiş siparişi sayar. Ciro ve ödeme dağılımı iadeyi doğru düşer.
- Gerçek kart/POS iadesi sistem dışıdır; muhasebe fiş (ÖKC iade belgesi) numarasını elle girer — mali müşavir onayı gerekir.
