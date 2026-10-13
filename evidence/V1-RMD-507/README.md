# V1-RMD-507 kanıtı

Sonuç: iade defteri ve `Payment` iade geçişleri hazır; gün sonu cirosu ve ödeme dağılımı iadeyi iade gününde düşer. Henüz hiçbir uç veya ekran bu servisi çağırmaz (V1-RMD-508).

## Ne değişti
- Migration 182: `payments.payment_reversals` (değişmez: güncelleme/silme veritabanında reddedilir; ödeme başına toplam iade ≤ onaylı tutar; gerekçe zorunlu; idempotency anahtarı benzersiz); `ck_payments_status_approved_amount_pairing` `Approved`, `Refunded`, `PartiallyRefunded` durumlarında onaylı tutarı zorunlu kılar.
- `Payment.ApplyRefund`: `Approved`/`PartiallyRefunded` → `PartiallyRefunded`, toplam onaylı tutara ulaşınca `Refunded`.
- `PostgresPaymentReversalRepository.AppendAsync`: çağıranın transaction'ında ödeme satırını kilitler, defter satırını ekler ve ödeme durumunu ilerletir; aynı anahtarla tekrar aynı satırı döndürür.
- Gün sonu cirosu ve ödeme dağılımı: iade edilmiş ödeme orijinal gününde tam sayılır, iade tutarı iade gününde düşülür.
- Faz B üst sınırı 182.

## Kapsam sınırı
- Hesap kapanış hesaplayıcısı ve tahsilat toplamları değişmedi: hesabın tüm ödemeleri iade edilince hesap iptal edilecek (V1-RMD-508); kısmi iade sonrası hesabın yeniden ödenmesi desteklenmez.
- `HasMoney` koşulları (iptal ve geri çağırma) iade edilmiş ödemenin ayırmasını hâlâ para sayar; V1-RMD-508 hesabı iade akışının içinde kendisi kapatır.

## Kanıt
- `test-runs.log`: PaymentAggregate 51/51 (8 yeni gerçek Postgres testi), Reporting.Payments 12/12, Host Reporting 8/8, manifest + migration zinciri 25/25.
- `mutations.log`: 13 mutant, hepsi KILLED.
