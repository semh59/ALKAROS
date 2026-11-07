# V1-RMD-886 incelemeleri

## qa-test-kalitesi (kritik/yüksek yok)
- Doğrulandı: kök neden (V1-RMD-782 satırı bilerek kaldırdı), `.git` yalnız backend-build aşamasında, çalışma imajlarına girmiyor.
- Düzeltildi: test yalnız tam `.git` satırına bakıyordu; `.git/`, `**/.git` biçimleri de reddediliyor (Python sözleşmesiyle aynı regex), N4 ve N5 mutantları öldü.

## qa-bulgu-dogrulayici (kritik/yüksek yok)
- İki iddia da doğrulandı; tests altında aynı bayat beklenti başka yerde yok; Python sözleşmesiyle çelişki yok.
- Düzeltildi: `.env` dışlamasını iptal eden `!.env` satırı reddediliyor (N6 öldü).
- Kabul edildi: `/.git`, `.git*` gibi daha uzak desenler (iki testte de ortak açık, düşük); aynı kuralın C# ve Python'da iki kez sınanması.
- Süreç notu: test V1-RMD-782'den beri kırmızıydı; o görevin kapanışında Host.Tests koşulmamış.
