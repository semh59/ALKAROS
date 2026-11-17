# V1-RMD-1150 karar kaydi (kasiyer denemesi 15: kalan maddeler)
Bu gorevde kod degismedi.

## 1. Odeme oncesi "Hesap" on fisi: YAPILMADI (urun ve mevzuat karari bekliyor)
Kodda durum: DualScreenApplication.Receipt.cs yalniz odenmis hesabin fisini verir (BILL_NOT_PAID, satir ~117 ve ~150-170); ilk basim "orijinal", sonrakiler "kopya" (BillReceiptPrintRepository, V1-RMD-887). Musteri fisindeki "Bu belge mali fis degildir" ibaresi split-payment.js:2344'te basilir; mali degeri yoktur ibaresi (EscPosTicketFormatter.NotFiscalNotice) mutfak fisindedir.
Neden yapilmadi (qa-mevzuat-uyum, repo kanitiyla):
- Repo on fisi hicbir belgede karara baglamamis (docs/plan/evidence aramasi: yok).
- Yeni bir belge turudur: orijinal/kopya sayacini tuketmemeli, ayri sayac/tur ister; on fisin yasal ibaresi ve icerigi repoda yok ("dogrulanamadi").
- Gercek usul + e-Fatura/e-Arsiv mukellefi isletmeler icin e-Adisyon (VUK 509 IV.12, gib-applicability-matrix.md:52-55) uygulanabilir olabilir; kagit on fisin e-Adisyon yerine gecip gecmedigi V20-CMP-001 ve Token/Beko adisyonu (V13-FSC-004) ile dogrulanacak, dis bagimlilik.
Semih'e sorulacak (urun/mevzuat): (a) on fis hangi isletme profillerinde acilsin? (b) ibare mali musavire onaylatilsin mi? (c) on fis orijinal/kopya sayacindan ayri mi olsun?

## 2. Satis ekranindan tek dokunusla satissiz cekmece: YAPILMADI
Sunucu satissiz acmada gerekce ister (CASH_DRAWER_REASON_INVALID) ve cekmece vurusundan ONCE denetim kaydi yazar (korunan degismez: kayitsiz acilis yok; V1-RMD-1138). "Tek dokunus" gerekcesiz olurdu ve bu guvenceyi bozardi. PosTerminal'da satissiz acma ucuna bagli hicbir ekran yok (src/Clients/PosTerminal/src icinde no-sale/cash-drawer aramasi: yok); form yalniz kasa sayfasinda (cash-session.js, #no-sale). Guvenli yol: satis ekranindan kasa sayfasindaki forma kisayol baglantisi (V1-RMD-1149 ile Kasa baglantisi artik geldigi yeri tasir); ayri gorev gerekir, urun karari (kisayol yeri) Semih'te.

## 3. Iade/iptal formu varsayilanlari: DEGISTIRILMEDI
split-payment.js: "Siparisi de iptal et" kutusu isaretsiz baslar (state.cancelOrder = false, satir 126); isaretlenince siparis iptali geri alinamaz (metin satir ~1808). Geri alinamaz bir eylemin varsayilan acik olmasi guvenlik geriletmesi olurdu; iptal gerekcesi bos iken dugme kapali. Formda "stoga donus" secenegi YOK (split-payment.js'te yalniz REFUND_STOCK_UNAVAILABLE hata cumlesi; src/Host'ta ReturnToStock/RestockItems yok): stok geri alimi sunucu kurali olarak (hazirlanmamis kalemler geri gelir, hazirlananlar fire) isler. Kullanim sikligina dayali bir "makul varsayilan" uydurulmadi (advisor).
