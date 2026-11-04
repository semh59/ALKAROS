# V1-RMD-812 - Müşteri ekranı arayüzü: tutarlı döküm, tamamlanan ödeme, uzaktan okunurluk ve kaydırma

- Task ID: V1-RMD-812
- Status: Planned
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G6 grubunun ekran parçası; sunucu verisi V1-RMD-810, eşleştirme V1-RMD-811. Kaynaklar: `7-birlesik-dogrulama.md` satır 14 ve 26; `3-tasarim.md` T-12, T-13, T-14, T-15, T-16, T-17, T-27; `9-1020-kasa-eslesme.md` F0413 (`Cashier.tsx:773`), F0803 (`CustomerDisplay.tsx:162` yenilik mantığı), F0836 (boş durum ekran koruyucusu testi), F1016 (ekran koruyucu marka rozeti kontrastı). Kodda doğrulanan kusurlar:

1. Döküm yanlış sıralı ve eksik (`CustomerDisplay.tsx:360-377`): bahşiş ve ücret satırı yok, "Hesap toplamı / Ödenen" Ara toplam ile İndirim arasına karışıyor, indirimli ve ikramlı kalem belli değil. Düzeltme: V1-RMD-810 verisiyle şu sırada: Ara toplam, İndirim (−), Bahşiş (+), Ücret (+, yalnız sıfırdan büyükse), Hesap toplamı, Ödenen (−), büyük KALAN/ÖDENECEK rakam; satırlar indirim öncesi tutarını, indirimli kalem "İndirim −x", ikramlı kalem "İkram" etiketini gösterir; İçindeki KDV en altta küçük. Kasiyer satış ekranındaki fiş (`Cashier.tsx:773`) aynı veriyle Bahşiş satırını gösterir, böylece Ara toplam − İndirim + Bahşiş = Toplam orada da tutar.
2. Tamamlanan ödeme ekranı yalnız "Teşekkür ederiz" der (`CustomerDisplay.tsx:279-288`); ödenen toplam görünmez: "Ödenen toplam" satırı eklenir.
3. "Canlı güncelleme · v{revision}" teknik etiketi kalkar (`CustomerDisplay.tsx:377`).
4. Okunurluk (`styles.css:445-452`): 1920x1080'de satır adı ve tutarı 21,6 px, alt satır ~13 px; satır yazısı uzaktan okunur boyuta (adı ve tutar en az 28 px, alt satır en az 16 px), toplam bloğu 1024x600'de tamamen görünür. Taşan satırlarda kaydırma göstergesi ("+N kalem daha") ve otomatik sayfa kaydırma; 8 satırdan fazlasında kaydırma göstergesi görünür; `prefers-reduced-motion` altında otomatik kaydırma yok.
5. Bağlantı bandı başlığı örtüyor (`styles.css:463`), belge 1080p'de 20 px taşıyor (`styles.css:440`), düşük kontrastlı metinler (`styles.css:455-462`): bant akışa alınır, kabuk görünür yükseklik içine sığar, kontrast ≥ 4,5:1; ekran koruyucu marka rozeti kontrastı ölçülür (F1016).
6. Boş durum ekran koruyucusu (F0836) ve yenilik sınırı (F0803) için testler.

## Owned surface

- `evidence/V1-RMD-812/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx (V1-RMD-097) — döküm, tamamlanan ekran, kaydırma.
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.test.tsx (V1-RMD-374) — döküm, okunurluk, kaydırma, ekran koruyucu testleri.
  - src/Clients/PosTerminal/src/styles.css (V1-RMD-042) — yazı boyutu, yerleşim, kontrast.
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-097) — fişte Bahşiş satırı.
  - src/Clients/PosTerminal/src/routes/Cashier.test.tsx (V1-RMD-374) — fiş testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Döküm sırası, satır indirimi/ikram etiketi, Bahşiş/Ücret satırları; tamamlanan ekranda ödenen toplam; sürüm etiketi kalkar.
2. Yazı boyutları ve yerleşim; 1920x1080 ve 1024x600'de toplam bloğu tam görünür; kaydırma göstergesi ve otomatik kaydırma.
3. Bağlantı bandı, taşma ve kontrast düzeltmeleri; kasiyer fişinde Bahşiş satırı.
4. Görsel doğrulama: Playwright ile 1920x1080 ve 1024x600 ekran görüntüsü (kendi TerminalId'si ile canlı yığında).

## Out of scope

- Sunucu verisi (V1-RMD-810), eşleştirme ve 429 (V1-RMD-811), kasiyer satış ekranının başka alanları.

## Dependencies

- V1-RMD-810
- V1-RMD-811

## Acceptance evidence

1. Vitest (PosTerminal) tam paket yeşil; tsc ve lint temiz; kırmızı sonra yeşil: döküm sırası ve etiketler, satır indirimi/ikram, bahşiş, tamamlanan ekran, sürüm etiketi yok, kaydırma göstergesi, indirgenmiş hareket, ekran koruyucu.
2. CSS ölçümü ve Playwright: yazı boyutları, 1024x600'de toplam bloğu görünür, belge taşması yok (ekran görüntüleri `evidence/V1-RMD-812/`).
3. Mutasyon: sıra, etiket, gösterge ve sürüm etiketi bozulunca test kırılır; `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi (qa-frontend, qa-ux-tasarim), bulgular giderilir, düzeltme sonrası doğrulama turu.
6. Semih'in elle deneyeceği senaryo: kasada 12 kalemli bir sipariş aç, %10 indirim ve bahşiş gir, kısmi öde; müşteri ekranı büyük yazıyla satırları, indirimi, bahşişi, ödenen ve kalanı sırayla gösterir, fazla kalemler kendiliğinden kayar.

## Handoff

- None
