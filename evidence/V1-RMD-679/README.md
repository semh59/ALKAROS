# V1-RMD-679 — Karar ekranında kişi adları

Sonuç: Bekleyen onaylar, devirler ve sıkılaştırmalar listesi artık kişinin görünen adını gösterir; ad bulunamazsa "Bilinmeyen personel" yazar, kimlik parçası asla görünmez.

## Kanıt
- `test-authorization-host.log`: Authorization Host testleri 11/11 geçti (gerçek PostgreSQL; `TheThreeListsCarryTheDisplayNamesOfThePeopleInThem` devralan ve veren için ayrı adlarla).
- `test-vitest.log`: PosTerminal vitest geçti, `tsc` temiz; iki yeni ekran testi (adlar görünür, ad yoksa "Bilinmeyen personel").
- `mutations.log`: N1–N4 (sunucu), C1–C3 (istemci) — 7/7 KILLED, dosyalar geri yüklendi.

## Açık / doğrulanamadı
- Tarayıcıda elle deneme yapılmadı (doğrulanamadı).
