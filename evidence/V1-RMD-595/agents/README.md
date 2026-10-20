# Ajan koşuları (V1-RMD-595, HEAD c41c1eec)

Ajanlar yalnız tmp/qa/ altına yazabiliyor ve evidence/ yazımı kanca tarafından engelleniyor; bu yüzden ham raporlar ana oturumda birleştirilip `../findings-raw.md` dosyasına işlendi (kaynak notasyonu ajan:no). Ham rapor metinleri oturum kaydındadır.

| Ajan | Kapsam | "N/M kontrol" | Ham bulgu | Token | Süre |
|---|---|---|---|---|---|
| qa-backend | tam | 14 tam + 4 kısmi / 18 | 15 | 78 bin | 4,9 dk |
| qa-veritabani | tam | 31/31 | 16 | 80 bin | 9,1 dk |
| qa-sinir-mimari | tam | 49/49 | 11 | 84 bin | 5,2 dk |
| qa-guvenlik-yetki | tam | 52/52 | 14 | 92 bin | 3,1 dk |
| qa-test-kalitesi | tam (mutasyon: 76 mutant, 38 yaşadı) | 30/30 | 21 | 114 bin | 36,4 dk |
| qa-operasyon-gozlemlenebilirlik | tam | 26/26 | 10 | 59 bin | 4,6 dk |
| qa-mevzuat-uyum | tam | 16/16 | 9 | 68 bin | 2,7 dk |
| qa-dokuman-bagimlilik | dar | 41/41 | 8 | 59 bin | 2,6 dk |
| qa-performans-dayaniklilik | dar | 14/14 | 10 | 53 bin | 2,8 dk |
| qa-bulgu-dogrulayici (Opus) | 26 bulgu | 26/26 | 2 yeni | 118 bin | 10,7 dk |

Toplam yaklaşık 805 bin token. Mutasyonlar repo dışı git worktree kopyasında yapıldı, kopya silindi; ana ağaç temiz (git status ve `git worktree list` ile doğrulandı).
