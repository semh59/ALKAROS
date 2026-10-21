# V1-RMD-597 — ajan başına kapsam ve doğrulama sonucu

Baseline: 231 dosya (scope-files.txt). Doğrulayıcı sonrası kayıplar yalnız çürüyen alt maddelerdir; hiçbir bulgu tamamen çürütülmedi.

| Ajan | Kontrol edilen | Ham satır | Çürüyen alt madde |
|---|---|---|---|
| qa-backend | 94/231 | 21 | 0 |
| qa-api-uclar | 24/231 (25 uç) | 19 | 0 |
| qa-sinir-mimari | 105/231 | 15 | 0 |
| qa-mevzuat-uyum | 42/231 + 7 | 15 | 1 (e-posta/telefon KVKK envanteri) |
| qa-ux-tasarim | 18/231 | 17 | 0 |
| qa-operasyon-gozlemlenebilirlik | 22/231 | 11 | 0 |
| qa-entegrasyon-cihaz | 20/231 | 15 | 0 |
| qa-test-kalitesi | 231/231 | 17 | 1 (T4 unlock pasif kullanıcı) |
| qa-performans-dayaniklilik | 231/231 | 20 | 0 |
| qa-dokuman-bagimlilik | 231/231 | 17 | 2 (DESIGN atıf, RMD-405 kırmızı test) |
| qa-veritabani | 101/231 | 18 | 0 |
| qa-frontend | 139/231 | 20 | 1 (izin kodları ham) |
| qa-guvenlik-yetki | 94/231 | 23 | 1 (Secure çerez) |

Not: dar kapsamlı ajanların (operasyon, entegrasyon, mevzuat, ux, api, sinir) kapsamadığı kalemler başka ajanlarca kapsandı; hiçbir dosya hiçbir ajan tarafından görülmemiş değil (test-kalitesi, performans, dokuman 231/231 taradı).
