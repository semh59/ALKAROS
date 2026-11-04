# V1-RMD-811 - Müşteri ekranı eşleştirme: sunucu süresine göre geri sayım, hız sınırında anlaşılır bekleme, sabit Türkçe hata cümleleri

- Task ID: V1-RMD-811
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G6 grubunun eşleştirme parçası. Kaynaklar: `7-birlesik-dogrulama.md` satır 30; `5-iletisim-agi.md` B8; `evidence/V1-RMD-739/findings/register.md` UI0188 (`CustomerDisplay.tsx:109` ham `reason.message`). Kodda doğrulanan kusurlar:

1. Eşleştirme kodunun yenilenme zamanı istemci saatiyle hesaplanıyor (`CustomerDisplay.tsx:45` dakika etiketi, `:121-122` zamanlayıcı: `expiresAt - Date.now()`). Saati ileri kayık bir ekranın her yeni kodu hemen "süresi dolmuş" sayılır, kod saniyeler içinde yenilenir; her yeni istek öncekini kapatır ve `pairing-create` sınırı (dakikada 10) dolunca ekran hata ekranına düşer. Geri kalmış saatte kod gerçek süresinden uzun gösterilir. Düzeltme: sunucu, kodun kalan süresini saniye olarak yanıtta verir (`ExpiresInSeconds`, kodun yaşam süresi sunucuda sabit); ekran geri sayımı istemci saatine bakmadan, yanıtın geldiği andan başlatır.
2. Hız sınırı (429) anlaşılır değil: eşleştirme kodu isteği 429 alınca ekran "Çok fazla deneme yapıldı..." sunucu cümlesini hata ekranında gösterir ve kendiliğinden yeniden denemez; ekran verisi 429 alırsa üstel geri çekilme bekleme süresini bilmeden dener. Sunucu zaten `Retry-After` başlığı gönderir. Düzeltme: istemci başlığı okur; ekran "Kısa sürede çok fazla deneme yapıldı. N saniye sonra kendiliğinden yeniden denenecek." cümlesini canlı geri sayımla gösterir ve süre dolunca kendiliğinden yeniden dener.
3. Müşteri ekranı hata metinleri sunucudan gelen ham `error.message` ile basılıyor (`CustomerDisplay.tsx:48,74,112`). `docs/UI_STYLE_GUIDE.md`: ham hata metni ekrana basılmaz; hata koduna göre sabit Türkçe cümle. Düzeltme: eşleştirme ve ekran verisi hataları durum koduna ve hata koduna göre sabit cümlelerle gösterilir.

## Owned surface

- `evidence/V1-RMD-811/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenContracts.cs (V1-RMD-148) — eşleştirme yanıtında kalan süre.
  - src/Host/DualScreen/DualScreenStore.Display.cs (V1-RMD-097) — kalan sürenin doldurulması.
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs (V1-RMD-090) — kalan süre testi.
  - src/Clients/PosTerminal/src/contracts.ts (V1-RMD-031) — eşleştirme yanıtı tipi.
  - src/Clients/PosTerminal/src/api.ts (V1-RMD-031) — `Retry-After` başlığı.
  - src/Clients/PosTerminal/src/api.test.ts (V12-QRT-001) — başlık testi.
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx (V1-RMD-097) — geri sayım, bekleme, sabit cümleler.
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.test.tsx (V1-RMD-374) — saat kayması, 429 ve cümle testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `PairingRequestCreated.ExpiresInSeconds` (sunucu sabitinden); sözleşme testi.
2. Ekran, kod geri sayımını yanıt anından, `performance` saatiyle değil zamanlayıcıyla başlatır; dakika etiketi aynı değerden.
3. `ApiError.retryAfterSeconds` (`Retry-After` başlığı, yoksa boş); 429'da ekran geri sayımlı bekleme cümlesi gösterir ve süre dolunca yeniden dener (kod isteği ve ekran verisi için).
4. Ekranda ham sunucu mesajı yok: ağ yok, sunucu hatası, hız sınırı, yetki ve genel durumlar için sabit cümleler.
5. Her düzeltme için önce kırmızı test, sonra yeşil; mutasyon turu.

## Out of scope

- Döküm ve okunurluk (V1-RMD-812); sunucu hız sınırı değerleri; kasiyer tarafı eşleştirme penceresi.

## Dependencies

- V1-RMD-810

## Acceptance evidence

1. Kırmızı sonra yeşil: Host testi (gerçek Postgres) ve vitest (PosTerminal): saati 10 dakika ileri/geri kayık ekranda kod tam süresi kadar kalır ve gereksiz yenilenmez; 429 + `Retry-After` ile geri sayım cümlesi ve süre dolunca yeniden deneme; ham mesaj yok.
2. PosTerminal tsc ve lint temiz; ApiConventions testi yeşil.
3. Mutasyon: istemci saatine dönmek, başlığı yok saymak, ham mesajı basmak testleri kırar; kaynak `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi, bulgular giderilir, düzeltme sonrası doğrulama turu.
6. Semih'in elle deneyeceği senaryo: müşteri ekranı bilgisayarının saatini 10 dakika ileri al; ekran açıldığında eşleştirme kodu 2 dakika kalır ve ardı ardına yenilenmez.

## Handoff

- None
