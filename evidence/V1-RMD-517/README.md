# V1-RMD-517 kanıtı

Sonuç: "Hesap Ödendi" ekranında "Fişi yazdır" ile mali olmayan bilgi fişi tarayıcı yazdırma penceresiyle basılır.

## Davranış
- Düğme fiş verisini `GET .../billing/bills/{billId}/receipt` ucundan okur (V1-RMD-516); okurken pasif kalır, tek okuma yapılır.
- Fiş: "Bilgi Fişi", "Bu belge mali fiş değildir.", hesap numarası ve tarih, kalemler, ara toplam, ayarlamalar (indirim "-" işaretiyle, bahşiş, hizmet), toplam, ödemeler (Nakit / Cari hesap / Kart / EFT / diğer; iade edilenler "İade edildi").
- Yazdırma stili yalnız fiş alanını basar; ekranda fiş alanı görünmez. Kalem adları kaçışlanır.
- Okunamazsa "Fiş verisi okunamadı. Tekrar deneyin." ve yazdırma açılmaz.

## Kanıt
- `test-runs.log`, `mutations.log`: 14 mutant, hepsi killed (ilk turda 2 hayatta kalan → test eklendi).

## Açık
- Gerçek yazıcıda/tarayıcıda yazdırma elle denenmedi; testler yazdırma çağrısını ve içeriği jsdom'da doğrular. 80 mm termal kâğıt genişliği (72 mm) tahmini bir ayardır, gerçek yazıcıyla doğrulanmadı.
- Mali fiş / yazarkasa (ÖKC) değildir.
