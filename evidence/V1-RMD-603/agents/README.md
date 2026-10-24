# Ajan raporları - V1-RMD-603 (SHA feb38368)
Baseline kapsam: 120 dosya (35 src, 40 test, 20 göç, 21 istemci). Her ajan ham raporunu yanıt olarak verdi; ana oturum dosyaya yazdı (ajanlar evidence/ yazamaz).

| Ajan | Rapor | Kontrol (kendi sayımı) | Ham kalem | Sapma/not |
|---|---|---|---|---|
| qa-mevzuat-uyum | qa-mevzuat-uyum.md | 120/120 envanter; 17 dosya satır satır | 12 | - |
| qa-guvenlik-yetki | qa-guvenlik-yetki.md | 74/120 | 14 | 46 konu dışı atlandı |
| qa-frontend | qa-frontend.md | 28/28 istemci dosyası | 23 | tsc/oxlint/static-apps kayıtları okundu |
| qa-api-uclar | qa-api-uclar.md | 20/20 uç | 17 | PaymentTender uçları kapsam dışı |
| qa-performans-dayaniklilik | qa-performans-dayaniklilik.md | ~46/120 | 14 | saf hesap kodu okunmadı |
| qa-sinir-mimari | qa-sinir-mimari.md | 12/12 sınır kalemi | 9 | ModuleBoundaries 17/17, consistency-audit temiz |
| qa-veritabani | qa-veritabani.md | 35/35 | 15 | 20 göç + depolar; geçici veritabanı silinemedi (güvenlik freni), 55432'de kaldı |
| qa-dokuman-bagimlilik | qa-dokuman-bagimlilik.md | 120/120 | 10 | lisans taraması temiz |
| qa-ux-tasarim | qa-ux-tasarim.md | 12/21 istemci dosyası tam | 21 | Playwright ile gerçek tarayıcı koşusu; PosTerminal ekranı görülmedi |
| qa-backend | qa-backend.md | 42 tam + 15 kısmi / 120 | 17 | 63 dosya açılmadı |
| qa-test-kalitesi | qa-test-kalitesi.md (+ mutants.tsv) | 120/120 | 22 | 59 mutant: 26 KILLED, 32 SURVIVED; limit nedeniyle 1 kez devam ettirildi |

Toplam ham kalem: 174. Kapsam sapması: güvenlik 74/120, performans ~46/120, backend 57/120, ux 12/21; eksik dilim README'de Doğrulanamayanlar altında.
qa-entegrasyon-cihaz koşmadı (dilimde dış cihaz/entegrasyon yok). qa-operasyon-gozlemlenebilirlik koşmadı (Billing'de arka plan işi yalnız kapanış; B020 ve B040'ta ele alındı).
