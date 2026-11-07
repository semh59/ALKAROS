# V1-RMD-882 sonucu

`RebuildAsync` silinmedi, gerçek akışa bağlandı: V0-DAT-004 her projeksiyon için "drift dedektörü + yeniden kurulum yolu" ister; silmek bu kuralı bozardı.
- `balance-reconcile` bakım işi (24 saatte bir; Güvenlik/Sistem ekranından elle de çalışır): defter toplamı ile `customer_account.balances` önbelleği farklı olan müşterileri `FindDriftedCustomersAsync` ile bulur (satırı eksik veya defteri olmayan artık satır dahil), her birini `RebuildAsync` ile defterden yeniden hesaplar. Sonuç iş özetinde: "N müşterinin bakiyesi defterle uyumsuzdu ve yeniden hesaplandı." / "Tüm müşteri bakiyeleri defterle uyumlu."
- Tur başına en fazla 500 müşteri (`MaxRepairsPerRun`); kalanlar sonraki turda.

## Kanıt
- Gerçek Postgres: BalanceProjection 35 test, Host CustomerAccounts 29 test (final-host.log), mutasyon sonrası yeniden derlenmiş koşu final3-host.log (BalanceReconcileJob + CustomerAging, 10 test), BalanceProjection final2-proj.log, exit 0.
- Mutasyon (mutations.txt): 10 mutant, hepsi öldü (karşılaştırma tersi, FULL->LEFT JOIN, Adjustment işareti, limit, eksik defter, onarım çağrısı yok, kayıt yok, yanlış özet, tur sınırı 5000, aralık 1 saat). Her mutasyondan sonra dosya cp+cmp ile geri alındı ve yeniden derlendi.
- Kırmızı test: onarım çağrısı olmayan (R6) ve dedektörü bozulmuş (R1, R2, R5) sürümde yeni testler başarısız.

## Davranış kararları
- Tek müşterinin onarımı hata verirse iş "Başarısız" olur (sessiz yutulmaz); aynı sıralama olduğu için ertesi gün yeniden denenir.
- Defteri olup bakiye satırı olmayan ve toplamı tam sıfır olan müşteri sapma sayılmaz (parasal etkisi yok).
- Yalnız `current_balance` karşılaştırılır; `last_transaction_at` sapması dedektöre dahil değil.

## Açık risk
- Dedektör günde bir kez tüm defter üzerinde GROUP BY çalıştırır; küçük işletme ölçeğinde kabul edildi, büyürse izlenmeli.
- İşaret kuralı SQL'de iki yerde (dedektör, `RebuildAsync`) ve trigger'da tekrar eder; kural değişirse üçü birlikte güncellenmeli.
