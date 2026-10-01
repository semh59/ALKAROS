# V1-RMD-484 - NFC siparişinde ekstra seçim ve zorunlu grup denetimi

Müşteri NFC siparişi artık kalem başına ekstra seçimi taşır; sunucu ad ve fiyatı katalogdan çözer, grup kurallarını (zorunlu, en çok) personel yoluyla (`TableDraftService`) aynı kodla denetler.
Önceden zorunlu ekstra grubu olan bir ürün seçimsiz sipariş edilip mutfağa gidebiliyordu.

## Değişiklikler

- `src/Host/Experience/NfcOrdering/*`: istek `Modifiers` taşır; ürüne ait olmayan ekstra, zorunlu grubun eksik kalması veya en çok seçimin aşılması 400 `VALIDATION_FAILED` (aynı Türkçe mesaj); ekstralar siparişe yazılır.
- `TableDraftService`: dört yardımcı `internal` oldu (kopya yok, tek kural).
- `src/Clients/PosTerminal` NFC sayfası: ürün satırında "Seç" paneli (tek seçimli grup radyo, çok seçimli onay kutusu, zorunlu grup tamamlanmadan "Sepete ekle" kapalı); aynı ürün farklı ekstralarla ayrı satır; toplam fiyat farkını içerir. Eski sepet biçimi (`{ürünId: adet}`) bozulmadı.

## Kanıt

- `tests-host.log`: NFC Host testleri (4 yeni: ekstra fiyatlanır ve kaydedilir, isteğe bağlı grup boş geçilir/zorunlu reddedilir, en çok aşımı, başka ürünün ekstrası).
- `tests-client.log`: NFC sayfası testleri (2 yeni: zorunlu grup engeli ve gönderilen gövde, aynı ürün iki ekstra = iki satır); `tsc` temiz.
- `mutation.log`: 4 mutant (grup kuralı denetlenmiyor, ekstralar satıra yazılmıyor, yabancı ekstra reddedilmiyor, panel zorunlu grubu beklemeden onaylıyor) kırmızı; dosyalar geri alındı, `fc /b` özdeş.
- `gercek-deneme.log`: gerçek Host (`serve`) + gerçek Postgres: Steak zorunlu pişirme seçimi olmadan 400, iki seçenek 400, başka ürünün ekstrası 400, trüf soslu 2 adet 200 ve toplam 850 (2 × (400 + 25)).

## Açık kalan

- QR yolu aynı boşluğa sahip: `V1-RMD-485`.
- Reçete maliyeti ve rapor kırılımı ekstraları hâlâ saymıyor.
