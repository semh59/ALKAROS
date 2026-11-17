# V1-RMD-1137 sonuc
Dogrulama: bulgu 1 gercekti (kara delik yazicida istek 2,13 sn; sunucuda kick disi yol ~0,13 sn). Raporlanan ~1,6 sn'lik ek istemci tarafi bu calismada sunucuda tekrarlanamadi (acik risk: canli tarayici olcumu yok).
Duzeltme: kicker siniri 2 sn -> 1 sn (satis ve Satis yok icin ortak); cash-tender'da kick hesap kapatmayla birlikte baslar; yanit drawerOpened (true/false/null) tasir, eski alanlar ayni; tekrar kick atmaz ve null doner.
Kanit: red.log (3 kirmizi, 2,13 sn), green.log (CashDrawerHttpTests 28/28), green-kicker.log 8/8, arch-apiconventions.log 17/17, mutations.txt M1-M5 killed (cp+cmp).
Not: kick-hesap kapatma eszamanliligi testle olculmez (kazanc ~0,1 sn, mutant yok). drawerOpened=true "darbe yaziciya verildi" demektir, cekmecenin fiziksel acildigini kanitlamaz (belgeye yazildi).
Acik risk: 1 sn sinir uykuda/Wi-Fi yazicida ilk baglantida yanlis "acilamadi" uretebilir (gercek cihazda olculmedi); kick sonrasi denetim kaydi (5 sn sinirli) yanita dahil, DB takilirsa gecikme uzar (odeme de ayni DB'ye bagli).
