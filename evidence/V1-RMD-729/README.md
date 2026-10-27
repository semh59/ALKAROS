# V1-RMD-729 - İndirim ve bahşiş sunucu sertleştirme (603 B006, B008–B011, B014, B024)

**Sonuç:** İndirim ve bahşiş uçları artık anahtarsız isteği, aynı anahtarın başka gövdeyle kullanımını, aşırı değeri ve uzun notu reddediyor; durum kilitten sonra okunuyor; her indirim/bahşiş hesap sürümünü artırıyor (eski sürümle kaydedilen bölme 409); grant ile onaylanan indirimde onaylayan ve grant kaydediliyor; yetkisiz rolün grant isteği açılmadan önce doğrulanıyor.

## Bulgu bulgu
- **B008 (doğru):** Anahtar boş/uzun (>128) ise 400. Aynı anahtar başka tür (indirim↔bahşiş) veya başka değer/gerekçe/nottaysa 409 `IDEMPOTENCY_KEY_REUSED`; aynı gövde yeniden denemede kayıtlı satırı döner.
- **B009 (doğru):** Hesap durumu ve tutarı artık `FOR UPDATE` kilidinden sonra okunuyor (indirim ve bahşiş); bahşiş de tahsilat ile aynı `payment-allocation` kilidini alıyor.
- **B010 (doğru):** Her indirim/bahşiş `bills.row_version`'ı artırıyor; eski sürümle bölme kaydı 409 `CONCURRENT_MODIFICATION` (önceden bölme doğrulaması 400 veriyordu). Kayıtlı bölme tasarımının bayat kalması bu sürüm kontrolüyle reddedilerek kapanıyor; tasarımı kendiliğinden yeniden hesaplama yapılmadı.
- **B011 (doğru):** `authorized_by` artık grantı onaylayan kullanıcı (politika otomatik onayladıysa isteyen); denetim kaydı `approverUserId` ve `grantId` taşıyor, aktör isteyen kasiyer olarak kalıyor (migration gerekmedi).
- **B014 (doğru):** Yüzde 0–100, tutar ve bahşiş en çok 1.000.000, not en çok 500 karakter; aşımda 400 (önceden NUMERIC taşması 500/503).
- **B024 (kısmen doğru):** Yetkisiz rol için grant açılmadan önce değer/aralık, hesabın varlığı ve durumu denetleniyor (olmayan hesap 404, geçersiz değer 400, ödenmiş hesap 409; grant açılmıyor).
- **B006 (çürütülmüştü, doğrulayıcı kararı):** Davranış doğru, yalnız test boşluğu; 737'de.

## Kanıt (logs/)
- Billing Host testleri: yeni 6 test (anahtar/sınır, anahtar çakışması, ön doğrulama ve grant sayısı 0, onaylayan kaydı, bölme sürüm çakışması, kilit sonrası okuma yarışı) ve mevcut 77 test (`logs/billing-full.log`).
- Mutasyon (`mutations.log`): 10/10 KILLED, kaynak geri yüklendi. İlk geçişte M1 derleme hatası verdi, M5 hayatta kaldı (yanlış test seçimi); mutant/test eşlemesi düzeltilip yeniden koşuldu (`logs/mutations-first-pass.log`).

## 728 düzeltmesinin tamamlanması
Tam koşuda `EndpointsEnforceSessionPermissionAndAtomicConcurrency` yine kırmızı çıktı: yanıt sırası motor sırası, veritabanı sırası (created_at, id); eşit zamanda ikisi ayrışıyor. 728'in tohum/ayırıcı düzeltmesi yetmemişti, test sıraya bağımlıydı. Üç assert sırasız karşılaştırmaya çevrildi (Billing 83/83).

## Açık
- Bahşiş tarafındaki `payment-allocation` kilidi için ayrı bir kırmızı test yok (kilit kaldırılan mutant denenmedi).
- Bahşişin kartla tahsili (B015) ve iptal edilmiş/ödenmiş hesaba bahşiş reddi testleri 737 ve PO kararında.
- Hata iletisi eşlemesi (anahtar eksik mesajı) 733'te.
