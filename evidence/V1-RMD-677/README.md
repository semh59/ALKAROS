# V1-RMD-677 kanıtı

- Sunucu: GET/PUT/DELETE `/api/v1/management/roles/policies...` (rol yönetimi izni; garson/kasiyer × indirim/ikram/iptal; yalnız auto_within; tutar ≤1000, adet 1-100, süre 1-24 saat; denetim olayları). Ekran: Yönetim > Personel ve roller > "Yetki limitleri" (düzenleme ve kaldırma onay sorar).
- Testler: test-roles-host.log (28 yeşil), test-apiconventions.log (5), test-vitest.log (24 dosya, 239 test), tsc temiz.
- Mutasyon: mutate-run.log + mutate-run2.log. 12 mutantın 11'i yakalandı (P4/P5/P7 ilk turda filtre testi kapsamadığı için hayatta görünüp test eklenerek yakalandı). P3 (açık sürüm kontrolü) eşdeğer: depo aynı korumayı eski sürümde `AuthorizationPolicyConcurrencyException` ile zaten yapıyor; açık kontrol okunabilirlik içindir.
- Doğrulanamadı: tarayıcıda elle deneme. Mevcut limit satırları yalnız garson indirimi için tohumlu (196).
