# V1-RMD-537 kanıtı

PosTerminal'de yakalanmamış JS hataları ve işlenmeyen promise reddi sunucuya raporlanır. İstemci: aynı hatadan en çok 3, sayfa başına en çok 20 rapor, `sendBeacon`; yalnız `eventId`, sabit `kind` ve sayfa yolu gider (mesaj, yığın, yazılan değer, token yok). Sunucu: `POST /api/v1/telemetry/browser-errors` kasiyer oturum çerezi ister (yoksa 401), oturum başına dakikada 30 rapor (ayrı kota, gerçek hata kaydını engellemez), olay kimliği ile tekilleştirir, kinds sabit kataloğa eşlenir (bilinmeyen `browser.unknown`), yol şablonlanır (kimlik ve sayılar `:id`/`:n`), terminal etiketi yalnız `terminal:<8 hane>`.

## Kabul koşulları
1. Oturumsuz çağrı 401 (ReportWithoutASessionIsRejectedWith401, gerçek Kestrel). Oran sınırı ayrı kota: gürültülü oturum 5 reddedilir, başka oturum kabul edilir (SessionQuotaIsolatesANoisyPageFromOtherSessions).
2. Aynı hata 50 kez → istemciden en çok 3 (vitest). Sunucuda aynı olay kimliği bir kez kabul edilir.
3. Metin sızıntısı: bilinmeyen/oynak `kind` metni hiçbir tabloya yazılmaz (UnknownKindFallsBackToAFixedCategoryAndNeverStoresClientText); istemci yükü yalnız 3 anahtar (vitest).
4. Test: sunucu 7/7, istemci 3/3 geçti; mutasyon 5 geçerli mutantın 5'i öldürüldü; Host DualScreenHostTests 6/6; `tsc --noEmit` temiz.

## Doğrulanamadı / sınır
- Geçerli kasiyer oturumuyla 204 yolu ve sunucu tarafı saatin esas alınması gerçek kimlik şeması kurularak test edilmedi (yalnız 401 yolu uçtan uca). Bu yoldaki oturum çözümleme mevcut `AuthenticateCashierByCookieAsync` çağrısıdır.
- `network_error` ve `render_error` türleri sunucuda tanımlı; istemci yalnız `js_error` ve `unhandled_rejection` üretiyor (ağ hataları için fetch sarmalayıcısı eklenmedi). Yönetici/mutfak ekranı ve vanilla uygulamalar kapsam dışı (553).
- Tarayıcıda canlı bir hata fırlatılıp olayın göründüğü elle gözlenmedi.
