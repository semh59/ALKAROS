# V1-RMD-835 sonuç

Kapatılan: tr-TR küçültmede büyük "I" noktasız "ı" olduğu için "isparta" aramasında "Isparta" çıkmıyordu. React ekranları (kasa ürün araması, katalog, çevrimiçi menü, masa) ortak `foldSearch` yardımcısını (`PosTerminal/src/search.ts`) kullanıyor; vanilla kasa (`cashier-app.js`) modül içe aktaramadığı için aynı tek satırlık katlamayı yerelde taşıyor (garson uygulaması V1-RMD-828'de aynı kalıpla yapıldı).

## Doğrulama
- PosTerminal vitest 103 dosya 1086/1086, tsc 0 hata; StaticApps 33 dosya 362/362.
- Kırmızı: 8 React testi ve 2 vanilla kasa testi önce düştü (yeni testler: yardımcı, Cashier.search-fold, katalog, çevrimiçi menü, masa, cashier-search-fold).
- Mutasyon 6 mutant (yardımcı, kasa, çevrimiçi menü, masa, katalog, vanilla kasa) hepsi öldürüldü, `cmp` ile geri doğrulandı.
- Atlanan test yok.

## İncelemeler (qa-frontend, qa-test-kalitesi)
Giderilen: vanilla kasada barkod tam eşleşmesi de katlıyor (`fold` modül kapsamına alındı, testli); testler "İSTANBUL", "ISPARTA", "ıspar" ve SKU tarafını kapsıyor; masa testi ikinci masanın elendiğini doğruluyor; vanilla testte sabit bekleme yerine koşullu bekleme. Mutasyon 8 mutant (SKU ve barkod dalları dahil) öldürüldü. Kabul edilen: vanilla katlama kopyaları (kasa, garson) modül paylaşamadığı için ayrı; kasada her çizimde katlama maliyeti ölçülmedi, katalog küçük.
