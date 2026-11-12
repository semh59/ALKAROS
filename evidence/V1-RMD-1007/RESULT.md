# V1-RMD-1007 - Kasa ekraninda Cikis calisir

Kok neden: Diger menusundeki dugmeye basinca menu ayni turda kapaniyor, sayfa tiklamayi okumadan menu (ve dugme) sokuluyordu; gercek tarayicida Cikis (ve Musteri ekranini esle, Rezervasyon istasyonu) hicbir istek gondermiyordu. jsdom'daki eski test bunu yakalamiyordu (senkron click).
Duzeltme: menu kapanisi tiklama islendikten sonraya ertelendi (Cashier.tsx).
Kanit: kirmizi test (menu kapanisi ile sayfa isleyicisi arasina tarayici gibi flush eklenir) eski kodda kirmizi, yeni kodda yesil; gercek tarayicida (Playwright) Cikis artik POST /auth/logout gonderiyor ve giris ekranina donuyor (cikis-sonrasi-1024x768.png). Istemci 1293/1293, oxlint/tsc/build 0. Mutasyon: 2/2 killed (mutations.txt).
