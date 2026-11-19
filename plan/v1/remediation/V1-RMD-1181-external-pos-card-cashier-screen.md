# V1-RMD-1181 - Harici POS kartı: kasiyer ödeme ekranında yöntem düğmesi, zorunlu fiş numarası alanı, Türkçe hata cümleleri

- Task ID: V1-RMD-1181
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Goal

Kasiyer "Hesap Ödeme" ekranında (`split-payment.js`) kart terminali TANIMLI DEĞİLKEN (`cardTerminalConfigured === false`) "Harici POS kartı" yöntem düğmesini görür; terminal tanımlıysa düğme çıkmaz ve normal kart akışı kalır (seçili yöntem terminal durumu değişince düşer). Düğme seçilince: Tutar, "POS fiş numarası" alanı (zorunlu, en çok 32 karakter, sunucu sabitiyle `ClientLimitLockTests` ile kilitli), yanında "Kart numarasını değil, yalnız POS fişindeki fiş numarasını yazın." uyarısı ve "Ödemeyi Ekle" düğmesi; alan geçersizken düğme pasif ve neden yazılıdır. Gönderim V1-RMD-1179 ucuna gider; işlem anahtarı fiş numarasını da içerir (çift tıklama tek kayıt); ödeme satırı, fiş ve iade metinleri `ExternalCard` için "Harici POS kartı" der; kart iadesi banka POS'unda yapılır notu gösterilir. Yeni hata kodları (`CARD_TERMINAL_CONFIGURED`, `EXTERNAL_CARD_SLIP_INVALID`, `EXTERNAL_CARD_SLIP_IS_CARD_NUMBER`, `EXTERNAL_CARD_SLIP_REUSED`) `ERROR_TEXTS` sözlüğüne sunucu cümlesiyle birebir eklenir; sözlük kilit testi yeni sunucu kaynağını da tarar. Düğme 48 px, yazı 14 px ve üstü; ham hata gösterilmez.

## Owned surface

- `evidence/V1-RMD-1181/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/error-dictionary-lock.test.js
  - tests/Host/Experience/CashSession/ClientLimitLockTests.cs
  - tests/Clients/StaticApps/split-payment-external-card.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (vitest, jsdom), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. Testler: terminal yokken düğme görünür, terminal varken görünmez ve seçim düşer; alan boşken/kısayken/kart numarası benzeri iken gönderim pasif; doğru gövde (`SlipNumber` kırpılmış) gider; başarıda alan temizlenir; sunucu hata kodları Türkçe cümleye çevrilir; aynı deneme aynı anahtarı kullanır; sözlük kilidi; `maxlength` sunucu sabitine eşit
4. `node tools/client-build-gate/clientBuildGate.js --base <başlangıç commit'i>` exit code 0; `tests/Clients/StaticApps` ilgili testleri ve `ClientLimitLockTests` exit code 0

## Out of scope

- Sunucu, migration, PosTerminal (React) ekranları, rapor ve belgeler; kart terminali varken harici POS kartına izin vermek

## Dependencies

- V1-RMD-1179
- V1-RMD-1180

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. İstemci üretim derlemesi çıktısı, plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1182
