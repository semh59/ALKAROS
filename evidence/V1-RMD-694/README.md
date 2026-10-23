# V1-RMD-694 - Kasa, fatura ve sipariş akışı düzeltmeleri

## Ne değişti

- **Hesap kapatma hatası (B023):** tahsilat sonrası hesabı kapatma hatası artık günlüğe düşüyor (`BillCloseFailed`). Hesap kapanıp siparişi tamamlanamazsa cevap "kapandı" kalıyor ve ayrı günlük satırı (`BillClosedOrderNotCompleted`) yazılıyor; müşteri ekranı bildirimi başarısızsa uyarı yazılıyor. Yutulan hata kalmadı.
- **Kasa hareketi ve kapanış yarışı (B051):** nakit/satış/kasa giriş-çıkış hareketi yazılırken oturum satırı paylaşımlı kilitleniyor ve oturumun Açık olması şart. Sayım başlatma ve kapanış aynı satırı güncellediği için yoldaki harekete bekliyor ve beklenen nakitte onu görüyor; oturum Açık değilse yeni hareket reddediliyor.
- **Aktif oturum ucu (B052):** `GetByTerminalIdAsync` (tüm geçmiş, 5000 sınırı) kaldırıldı; `GetActiveByTerminalIdAsync` yalnız Açık/Sayımda/Kapanıyor oturumu okuyor (kasa açma ilkesi de bunu kullanıyor).
- **İkinci havuz bağlantısı (B053):** sipariş okuması mutasyonun bağlantısı ve işlemi içinde yapılıyor (`PostgresOrderRepository.GetByIdAsync(id, connection, transaction)`); tek bağlantılı havuzda tüm sipariş mutasyonları çalışıyor.
- **Satır birleştirme (B056):** aynı ürün yalnız çıplak satıra (modifiyer, not, koltuk, kurs, indirim yok, aynı güncel fiyat) birleşiyor.
- **Müşteri ekranı (B057):** terminal işaretçisi, sipariş ve satırlar tek tutarlı okumada (RepeatableRead); ikram satırı modifiyerleri de içeriyor.
- **Olmayan oturum (B086):** `expected-cash` ve diğer oturum uçları olmayan oturumda 404.
- **Fiş yöntemi (B102):** onaylı kart ödemesi fişte `Card` ("Banka kartı"); kalan `Manual` etiketi "EFT / diğer".
- **Kasa hareketi tekrarı (B130):** aynı anahtar farklı tutar/yönle gelirse 409 `IDEMPOTENCY_KEY_REUSED`; yarış dalı da aynı denetimden geçiyor.

## Kanıt

- `mutate.mjs` / `mutations.log`: 25 mutant. İlk geçişte A2 (Kapanıyor durumu), O1 (modifiyerli satır), O6 (fiyat), Q2 (modifiyer adedi) sağ kaldı; testler eklendi, ikinci geçişte KILLED. Diğerleri ilk geçişte KILLED, kaynak her seferinde geri yüklendi.
- Yeni testler: `BillClosureLoggingTests`, ledger kilit/durum testleri, oturum aktif sorgusu, `CashSessionHttpTests` (404, anahtar yeniden kullanımı), `DualScreenStoreTests` (birleştirme, tek bağlantı havuzu, ikram+modifiyer, nesne durumları), fiş kart testi, istemci fiş testi.
- Tam test koşusu `logs/` altında.

## Yapılmayanlar / doğrulanamadı

- **EFT fişte ayrı değil:** EFT ödemesini ayıran bir kayıt yok; "EFT / diğer" altında kalıyor.
- **Eşzamanlı aynı anahtar yarışı:** yarışı kaybeden dalın kendisi (benzersiz ihlali sonrası okuma) deterministik test edilemedi; kodu tek yola indirgendi, ama o satırın tek başına mutasyon kanıtı yok.
- **Sayım sonrası Sale:** nakit satış artık oturum Açık değilken reddediliyor; yarışta kaybeden satış 409 görür. Nakit alındıysa ve bu durumda kalırsa elle düzeltme gerekir (kapsam dışı, PO kuralı gerekir).
- Kart tahsilatı yer tutucu ve "kart çekilmedi" kuralı kapsam dışı (görev metni).
