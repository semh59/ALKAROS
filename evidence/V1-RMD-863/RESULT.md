# V1-RMD-863 sonuç (F0094, F0095, F0450)

Önce: rapor yöntemi eleme ile çıkarıyordu ve card_settlement_attempts LEFT JOIN edildiği için iki denemeli ödeme (reddedilmiş + onaylanmış) iki kez sayılıyor, iade toplamı da katlanıyordu; tek bir reddedilmiş deneme EFT ödemesini "BankCard" yapıyordu; cari hesaba yazılan ödeme ve elle onaylanan kart ödemesi "Eft" çıkıyordu (red.log: 3 yeni test düştü).
Şimdi: yöntem Host PaymentMethodLookup ile aynı kuralla, EXISTS ile bulunur (cari hesap "CustomerAccount", nakit, onaylı deneme ya da onaylı elle onay "BankCard", kalan "Eft"); GROUP BY'daki 20000 satırlık anlamsız LIMIT ve ona bağlı hata, yöntem sayısına (4) indirildi (tutarlılık denetimi her SELECT için sınır ister). İade toplamı aynı kuralı kullanır.

Testler (Reporting.Payments): 16/16 geçti (final-module.log); Host PaymentSettlement 3/3 (final-host.log).
Mutasyon (cp+cmp geri alındı, her biri yeniden derlenerek): onay koşulu, elle onay koşulu, cari dalı, nakit dalı, Pending, RequiresReconciliation, iki öncelik sırası: dördü de 1 test düşürdü (mutations.txt).
Sınır: kural Billing tarafındaki PaymentMethodLookup ile iki kopyadır (modül sınırı); eşitliğini sınayan test yok, yeni bir yöntem eklenirse ikisi birlikte değişmelidir. Elle onay "Pending" olan ödeme EFT sayılır (ödeme onaylı ama kart onayı yok; Billing tarafıyla aynı).
Açık risk (qa-backend, orta): manual_card_confirmations için payment_id üzerinde yalnız Pending'i kapsayan kısmi indeks var; Approved araması bu tablo büyürse ödeme başına tarama yapar. Tablo yalnız elle kart onaylarını tutar (küçük); indeks yeni migration ister, bu görevin kapsamı dışında, rapora yazıldı.
