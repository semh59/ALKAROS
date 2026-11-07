# V1-RMD-887 incelemeleri

## Tur 1 - qa-api-uclar (kod okuması)
Kritik/yüksek yok.
- Orta: orijinal hakkı commit edildikten sonra fiş verisi okunuyordu - düzeltildi: fiş verisi talepten önce okunur.
- Düşük: ödenmiş kontrolü işlem dışındaydı - düzeltildi (koordinatör şartı): kontrol, talep ve denetim aynı işlemde satır kilidi altında; test ve satır kilidi mutantı eklendi.
- Düşük, kabul: `BILL_NOT_PAID` hata sözlüğünde yok (eski reprint ucunda da yoktu, istemci sabit metin gösterir).
- Temiz: kaldırılan uç başka çağıran bırakmadı (repo taraması), yetki/terminal sahipliği, rate limit, DI, GET geriye uyumu, migration 223 up/down, idempotency allowlist.

## Tur 2 - qa-uctan-uca-akis (kod okuması)
Ana akış tutarlı. Bulgular:
- Orta: yanıt kaybı/zaman aşımında orijinalin harcanması RESULT.md'de yoktu - eklendi, idempotency_allowlist gerekçesine de yazıldı (kabul edilen ürün riski).
- Orta: testler claim-denetim aynı işlem, basım yapan kullanıcı sütunu ve okuma sırasını kilitlemiyordu - eklendi: AFailedAuditWriteTakesTheOriginalClaimWithIt..., `receipt_first_printed_by` assert'i, paid/lock testleri; yeni mutantlar öldü. İki terminal aynı hesap testi eklenmedi: hesabı başka terminale ait olan kasiyer filtreden geçemez (BillTerminalAccessTenderHttpTests rota matrisi).
- Düşük (giderildi): mutasyon 7. satır logu (vitest çıktısı), manifest yorumu 223, hata mesajı ayrımı.
- Bilgi: GET basımdan önce isCopy=false döner (RESULT.md'ye yazıldı, bugün GET'ten fiş çizen istemci yok).
- Ürün kararı, dokunulmadı: düğme yanına "ilk basım orijinaldir" uyarısı.

## Doğrulama turu - qa-bulgu-dogrulayici
Sonuç aşağıda.
Doğrulayıcı beş iddiayı kodda doğruladı; bulgular ve yanıtlar:
- Orta: planın kabul koşulu "kırmızı koşu" yoktu - alındı (red-1.log).
- Düşük (giderildi): `FOR UPDATE` yerine `FOR NO KEY UPDATE` (kilit mutantı yeniden koşuldu), denetim hata testi 500 kodunu sınar, mutasyon açıklamaları ve istemci vitest çıktısı kayda eklendi, eski iki mutasyon logu silindi.
- Düşük, kabul: "okuma hatası orijinali harcamaz" sırasını sınayan test yok (okuma sırası kodla doğrulandı, `response-drops-original-time` mutantı yanıt alanını yoklar); uçtaki NotFound/NotPaid dalları HTTP'de ön kontrolün arkasında kalır, depo testi bu dalları doğrudan sınar.
- Plan Goal metnindeki `UPDATE ... WHERE ... IS NULL` ifadesi ile kod arasındaki fark (kilit altında kontrol sonra güncelleme, fiş verisi önce okunur) bilinçli iyileştirmedir; plan aktifken değişmez.
Son durum: doğrulandı.
