# V1-RMD-730 - KDV, fiş ve bölme doğruluğu (603 B012, B013, B025)

**Sonuç:** Karışık KDV oranlı hesapta indirimin KDV'si artık hesabın kendi oranında (yuvarlanmamış) çıkıyor; bilgi fişi satırların toplamıyla tutuyor ve KDV dökümü (oran başına matrah ve KDV) basıyor; bölmede hiçbir kişinin KDV'si kendi tutarını aşamıyor ve toplam KDV kuruşu kuruşuna dağılıyor.

## Bulgu bulgu
- **B012 (doğru):** İndirimin KDV'si `Bill.EffectiveTaxRatePercent` (yuvarlanmamış ağırlıklı oran) ile hesaplanıyor; 1000 TL %1 + 1000 TL %20 hesapta %50 indirimin KDV'si 88,29 (iki hane oranla 88,26 çıkıyordu). Satır bazlı dağıtım yapılmadı: her satırın KDV'si zaten kendi tutarına orantılı, fark yalnız oran yuvarlamasındaydı; modül testi bunu doğruluyor.
- **B013 (doğru):** `/receipt` ara toplamı yazdırılan satırların toplamı (`bill.PayableAmount`); önceden ikram/satır indirimi öncesi tutardı ve fiş toplamı tutmuyordu. Satırlar `lineDiscount` taşıyor, yeni `taxLines` oran başına matrah/KDV/brüt veriyor ve toplamı ayarlanmış KDV toplamıyla birebir (bahşiş KDV'siz). Kasa fişi "İndirim / ikram" satırını ve KDV tablosunu basıyor. Tohum KDV'si sıfır kalıyordu; yeni testte %10 KDV ve satır indirimi var.
- **B025 (doğru):** Eşit, tutar ve özel bölmede KDV artık en büyük kalan yöntemiyle (`BillMath.Apportion`) tam kuruş dağıtılıyor; önceki "kalan son kişide" kuralı 0,01 tutarlı kişiye 0,02 KDV verebiliyordu. Veritabanına `tax <= tutar` CHECK'i eklenmedi (migration gerekmedi, üretim verisine karşı risk); koruma motorda ve testte. "Vergisiz bahşiş paydada" iddiası: her kişi aynı oranda pay alıyor, toplam korunuyor; bahşiş payı ayrıca modellenmedi, ürün davranışı değişmedi.

## Yüzey genişletmesi
Fiş uç noktası (`src/Host/DualScreen/DualScreenApplication.Receipt.cs`) ve testi görev yüzeyinde yoktu; ayrı plan commit'iyle (10214f82) eklendi.

## Kanıt (logs/)
- Modül: SplitDesign 31/31, Adjustments 19/19, BillFoundation 44/44; ApiConventions 5/5; Host Billing 83/83; PaymentTender BillReceipt 7/7 (yeni 1); StaticApps 123/123 (yeni 1; 727'den beri ilk tam koşu).
- Mutasyon (`mutations.log`): 8/8 KILLED, kaynak geri yüklendi.

## Açık
- Fiş KDV dökümü hizmet bedeli/kuver (vergili ücret) varsa onları da orantılı dağıtır; bu ücretleri oluşturan uç yok (yasal kısıt).
- PosTerminal fişi yok; yalnız Kasa sayfası basıyor.
