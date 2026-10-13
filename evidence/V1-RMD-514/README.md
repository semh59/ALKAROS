# V1-RMD-514 kanıtı

Sonuç: hesap iptali, iade isteği ve iade reddi gerekçeleri hazır listeden seçilir; "Diğer" serbest metin açar.

## Davranış
- Liste: Müşteri vazgeçti, Yanlış sipariş, Mutfak hatası, Ürün sorunlu, İkram, Yanlış tutar girildi, Diğer.
- Seçim yapılmadan ilgili düğme pasif; hazır gerekçe tek dokunuşla; "Diğer" seçilince metin alanı açılır ve boşken düğme pasif kalır.
- Hem kasa sayfasında hem PosTerminal iade panelinde aynı liste. Sunucuya gerekçe yine metin olarak gider (sözleşme değişmedi).

## Kanıt
- `test-runs.log`, `mutations.log`: 13 mutant, hepsi killed.

## Açık
- Liste sabit; müdürün düzenlediği liste ve gerekçe bazlı rapor kapsam dışı.
- Gerçek tarayıcıda elle deneme yapılmadı.
