# V1-RMD-867 incelemeler

1. qa-backend: kritik/orta yok; düşük 3: iki ondalıktan fazla tutarda yanlış çakışma (düzeltildi: yuvarlama + test), istisna için Host eşlemesi yok (bugün tetiklenemez: çağıranlar her seferinde yeni kaynak kimliği verir; ilk sabit kimlikli çağıranla birlikte eşlenmeli), Adjustment/Refund yeniden kurulum testi yoktu (eklendi).
2. qa-test-kalitesi: yüksek 1 (bekleme testleri kilit mutantını yakalamıyor: RESULT'taki "deterministik" ifadesi düzeltildi, yoklama pg_stat_activity ile yapıldı, olasılıksal test büyütüldü), orta: Task.Delay (kaldırıldı), last_transaction_at ve müşteri süzgeci assert yok (eklendi, 2 yeni mutant öldü), yuvarlama mutantları (öldü). Kalan düşük: eşzamanlı aynı anahtar yazımı ve iptal/geri alma testi yok.
3. Doğrulama turu: 4 test projesi yeşil, 11 mutant öldü.
