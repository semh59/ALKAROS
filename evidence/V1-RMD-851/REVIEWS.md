# V1-RMD-851 incelemeler

1. qa-performans-dayaniklilik: orta bulgular (4,6 sn kaynaksız ve ölçülen en kötü 12 sn; mutasyon 335/370 ve varsayılan mutasyon yok; 20 sn payı zayıf) -> sınır 30 sn'ye çıkarıldı, varsayılan 5 sn + 40 yükle bildirilen hata yeniden üretildi (waiter-app ilk üç test 5026/5008/5215 ms), aynı yükte düzeltmeyle tek dosya 13/13, 24 yükle tam koşu 370/370.
2. qa-test-kalitesi: orta bulgular (RESULT sayılarının kanıt dosyalarında olmaması; işçi başlatma hatası iddiası; planlanan 4 sn kırmızısının üretilememesi) -> ham ölçüm dosyaları `measure-*` olarak eklendi, işçi hatası iddiası yumuşatıldı, plandan sapma RESULT'ta yazıldı; 30 sn sınırının gizleyebileceği takılmalar RESULT'ta kayıtlı.
Doğrulama turu: iki incelemenin sayısal iddiaları evidence JSON/log'larıyla yeniden karşılaştırıldı (5026/5008/5215, 2164, 2456, 506, 5870, 9887, 12143, 370/370); çürütülen bulgu yok.
