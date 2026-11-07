# V1-RMD-883 incelemeleri

## Tur 1 - qa-veritabani (kod okuması ve matematiksel çıkarım)
SQL FIFO hesabının eski bellek içi hesaplayıcıyla denkliği kanıtlandı (sıralama, kredi emilimi, negatif düzeltme, baş çıkarımı, kova ve vade sınırları). Kritik/yüksek yok.
- Orta: vade günleri ayrı sorguyla okunuyordu, okuma ile sorgu arasında vade değişirse "vadesi geçmiş 0" sessizce yanlış çıkabilirdi - düzeltildi: 1..365 günün hepsi için kesim anı sorguya geçilir, ayrı okuma kalktı.
- Düşük: ekran toplamında vadesiz müşteriler 0 sayılıyordu - düzeltildi (vadeli müşteri yoksa "—").
- Düşük, kabul: uç noktadaki müşteri başına profil okuması (N+1) kapsam dışı; 500 bin+ satır için EXPLAIN ölçümü yapılmadı (RESULT.md açık risk).

## Tur 2 - qa-test-kalitesi
Eski 8 kabul koşulu eksiksiz taşındı. Bulgular ve sonuç:
- Orta: müşteri sınırı (MaxCustomers) testsizdi - eklendi (sınırda geçer, sınır+1 hata verir); `+1` mutantı öldü.
- Orta: aynı `occurred_at` sıralama testi önerildi - eklenmedi: aynı anlı satırların sırası FIFO sonucunu değiştirmez (kuyruk toplamları sıradan bağımsız), `id` yalnız belirlenimcilik içindir; tartışılabilir, doğrulayıcıya itiraz hakkı bırakıldı.
- Düşük (giderildi): vadesi olup vadesi geçmiş borcu olmayan müşteri (0, boş değil), vade 1 ve 365 sınırları, frontend toplam 0/boş ayrımı ve satır hücreleri; `Range(1,364)` ve `0m->null` mutantları öldü.
- Düşük, kabul: 500k testi yalnız doğruluk sınar (süre/plan ölçülmedi); koleksiyon tanımı aging test dosyasında.

## Doğrulama turu - qa-bulgu-dogrulayici
Sonuç aşağıda.
Doğrulayıcı kod ve kanıtları okudu; itiraz yok (aynı anlı sıralama, EXPLAIN, N+1 kararları). Altı düşük bulgu: toplamda 0/boş mutantı (test eklendi, mutant öldü), eski kodla koşmuş vade mutantları (son kodda yeniden koşuldu), eski tsc/vitest log adları (RESULT.md düzeltildi), kırmızı ara log (silindi), 365 sabitinin tekrarı (sabit kullanılıyor), "90+" etiketi (düzeltildi). Son durum: doğrulandı.
