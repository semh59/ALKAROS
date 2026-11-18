# V1-RMD-1152 sonuc: satis ekraninda "Satis yok" baglantisi

Karar (Semih, 2026-11-18): satis ekraninda "Satis yok" baglantisi kasa sayfasindaki gerekce formuna goturur; form satis ekranina konmaz; 1149'daki returnTo ile geldigi yere doner.

## Yapilan
- PosTerminal menusunde (Diger) "Satis yok" baglantisi: yalniz kendi acik kasa oturumunda ve sunucu cekmecenin acik ve kullanici yetkili oldugunu soyluyorsa gorunur. Bagli adres: `/cashier/payments/cash-session/index.html?returnTo=<gelinen sayfa>&open=no-sale`.
- Kasa sayfasi `open=no-sale` ile gerekce formunu dogrudan acar (bir kez; parametre adresten silinir). Form kullanilamiyorsa nedeni Turkce soyler. Geri baglantisi satis ekranindan gelindiyse "Satis ekranina don" der.
- Sunucu degismedi: gerekce zorunlu ve denetim kaydi cekmece vurusundan once yazilir. Gerekcesiz tek dokunusla cekmece acilmaz.
- Baslik: kasa durumu cekmece durumunu beklemez (ayri ve en yeni okuma kazanir).

## Kapsam kararlari
- Baglanti menude (Diger), ustte degil: ust cubuk "en cok dort oge" testiyle sinirli (Cashier.header.test.tsx).
- Yalniz satis ekrani (Cashier.tsx); masa ve diger rotalar karar disi.
- Form iptali ve basarili aciliştan sonra otomatik donus yok (plan Out of scope); kasiyer ustteki "Satis ekranina don" baglantisini kullanir.

## Kanit
- Kirmizi: `red-static.log` (9 test), `red-pos.log` (33 test); yesil: `green-static.log`, `green-pos.log`, `pos-full.log` (128 dosya, 1553 test), `client-build-gate.log` (lint, tip, vite build exit 0), `plan-audit.log` (0 hata, 0 uyari), `consistency-audit.log` (clean).
- Mutasyon: `mutations.txt`, 26 mutant, 26 oldurulde (kopyala-geri al-cmp her birinde). `if (false)` ve duzenli ifade kaliplarinin ilk denemeleri gecersiz kaldigi icin yeniden denendi.
- qa: `REVIEWS.md` (qa-frontend, qa-ux-tasarim; bulgularin hepsi duzeltildi veya gerekceyle birakildi).

## Acik risk
- `tests/Clients/StaticApps` tam kosuda `split-payment-discount-pending` ve `split-payment-approval-restore` testlerinden bir kismi kirmizi (`static-full.log`). Bu gorevden once de kirmiziydi: temiz HEAD'de ayni dosya tek basina kosuldugunda 1 test basarisiz oldu. Bu gorevin dosyalariyla ilgisi yok; ayri gorev gerekir.
- Menude olmasi nedeniyle "Satis yok" iki dokunus (menu + baglanti) ve gerekce sonrasi donus icin bir dokunus daha ister; ust cubuga alma ve otomatik donus urun karari olarak sonra konusulabilir.
