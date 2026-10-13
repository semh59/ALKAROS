# V1-RMD-506 kanıtı

Sonuç: ödenmemiş hesap gerekçeyle iptal edilebiliyor; iptal sonrası aynı siparişten yeni hesap açılabiliyor; garsona geri çağırma yolu da aynı düzeltmeden yararlanıyor.

## Ne değişti
- Migration 180: `bills.bill_number` ve `bill_items.order_item_id` benzersizliği yalnız aktif kayıtlarda geçerli (kısmi benzersiz index); `bills.cancellation_reason`, `bills.cancelled_by`, `bill_items.superseded_at`.
- Migration 181: `bills.cancel` izni kasiyer, süpervizör, müdüre atanır (garson almaz).
- `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/cancel`: hesap kilidi altında; gerekçe 1–500 karakter zorunlu (400 `REASON_REQUIRED`); ödeme ayırması, beklemedeki ödeme veya `Paid` durumu varsa 409 `BILL_HAS_PAYMENT`; zaten iptal edilmişse 409; masa işaretçisi temizlenir; `bill.cancelled` denetim olayı yazılır.
- `CancelActiveBillsForOrderAsync` (geri çağırma) artık hesap satırlarını da `superseded_at` ile işaretler ve yalnız iptal edilen hesap sayısını döndürür.
- Faz B üst sınırı 181; `bills.cancel` etiketi yönetim ekranı rol listesinde Türkçe.

## Kanıt
- `test-runs.log`: Billing Host 33/33 (10 yeni gerçek Host + Postgres testi), Identity authorization 220/220, Host migration/manifest 174/174, BillFoundation 44/44, TableDraft (geri çağırma) 88/88.
- `mutations.log`: 15 mutant, hepsi KILLED (iki sağ kalan mutant için test eklendi).

## Açık risk
- Ödenmiş hesap iptali ve iade bu görevde yok (V1-RMD-507..509). Arayüz düğmesi yok (V1-RMD-509); uç şimdilik yalnız API.
- Elle seçilmiş migration listesi taşıyan başka test projeleri bu kolonlara bakmıyor; yeni migration'a bağlı yalnız BillFoundation ve TableDraft listeleri güncellendi.
