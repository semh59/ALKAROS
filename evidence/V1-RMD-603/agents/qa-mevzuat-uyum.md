# qa-mevzuat-uyum - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 120/120 kalem envanterde; 17 dosya satır satır, 82 src/göç dosyası anahtar sözcükle, 21 test/istemci dosyası mevzuat grep'iyle tarandı. Kaynak: docs/compliance/money-tax-business-date.md, void-complimentary-discount-policy.md, refund-ledger.md, card-tip-scope-decision.md, restaurant-fees-and-tips.md, kvkk-retention-runbook.md, V1-WTR-020.

| No | Önem | Kanıt | Sorun | Etki |
|---|---|---|---|---|
| M-1 | Orta (karar) | BillingSplitStore.cs:114-115 | İndirim KDV'si karışık oranlı hesapta tek ağırlıklı ortalama oranla (2 hane) hesaplanıyor; doküman satır bazlı orantılı dağıtım ve satır başı yuvarlama istiyor. 50 TL %10 + 50 TL %20 satır, %10 indirim: kodda 1,29, satır bazında 1,28. Karışık oranlı indirim testi yok (grep 0). | AdjustedTaxTotal, bölme ve fişte 1 kuruş KDV sapması |
| M-2 | Orta | split-payment.js:1344-1377; DualScreenApplication.Receipt.cs:87 | Bilgi fişi API'si TaxTotal döndürüyor, fiş HTML'inde KDV satırı yok. Kabul kriteri 5 ("fişte KDV doğru") karşılanmıyor. Belge "mali fiş değildir" etiketli. | Müşteri belgesinde KDV görünmüyor |
| M-3 | Orta | Bill.cs:126-134; Receipt.cs:86; split-payment.js:1359-1364 | Fişte "Ara toplam" satır indirimi ve ikram değerini içeriyor, satırlar indirimli brüt yazılıyor. Satır indirimi/ikram varsa "Ara toplam - indirim + bahşiş ≠ Toplam". | İkramlı/satır indirimli fişte toplamlar tutmuyor |
| M-4 | Orta (karar) | AdjustmentCalculator.cs:66; BillPaymentClosureCalculator.cs:61-66; card-tip-scope-decision.md | Bahşiş ödenecek tutara ekleniyor; tahsilat tavanı bahşiş dahil; ApplyTipAsync ödeme yöntemine bakmıyor. V13-GOV-004 kart bahşişini kapsam dışı bırakmıştı. | Fiilen kart üzerinden bahşiş tahsili mümkün |
| M-5 | Orta | BillAdjustment.cs:230 (CreateServiceFee, isKuver); 021 CHECK ServiceFee/Kuver; AdjustmentCalculator.cs:44-48 | Zorunlu servis ücreti/kuver yalnız "bağlanmamış" olmak ve yorumla engelli; CHECK ve hesaplayıcı kabul ediyor; uç nokta 0; koruyucu mimari/birim test yok. V0-CMP-004 kuveri "menüde ilan edilirse serbest", V1-WTR-020 yasak sayıyor. | Yasak ücret satırı kolayca açılabilir; kaynak çelişkisi |
| M-6 | Orta | PaymentRefunds.cs:208-231, :131 | 3x3,33 (9,99), %10 indirim, ödenen 8,99: üç kalemin tamamı ürün bazlı iade = 3 x round(3,33 x 0,8999) = 9,00 > 8,99, reddedilir (tahmin, çalıştırılmadı). Son kalemde kalan kuruş kuralı yok. | Tüm kalem iadesinde kuruş farkı; geçici yol tüm ödeme iadesi |
| M-7 | Orta | PaymentRefunds.cs:219-231; refund-ledger.md | İade kaydı KDV kırılımı ve mali belge bağlantısı taşımıyor (Billing diliminde "fiscal" 0 sonuç). Dış bağımlılığa bağlı olabilir. | İade edilen KDV Billing kayıtlarından türetilemez |
| M-8 | Orta | PaymentRefunds.cs:440-470 | Ödenmiş hesap tam iade edilince Cancelled + "Ödeme iade edildi", satırlar superseded. Void-refund ayrımının raporlara etkisi doğrulanamadı. bilinen (PO 2026-10-13) | Rapor etkisi belirsiz |
| M-9 | Orta | kvkk-retention-runbook.md; PostgresRetentionExecutionService.cs (billing 0) | bill_items.notes, bill_adjustments.notes, bills.cancellation_reason, refund_requests.reason/decision_note anonimleşmiyor; runbook "Known gap" yalnız void/comp sebebini sayıyor. | KVKK süresi sonrası serbest metin kalıyor |
| M-10 | Düşük | SplitEngine.cs:46,127,315 | Doküman sapması: bölmede KDV payı yarıya değil aşağı yuvarlama + kalan; toplam tutuyor. | Kişiler arası en çok kuruş |
| M-11 | Düşük | SplitEngine.cs:33,103,299 | Vergisiz bahşiş de KDV dağıtım paydasında. | Kuruş mertebesi |
| M-12 | Düşük | BillingSplitContracts.cs:133-136; BillingSplitStore.cs:198-212 | Bahşişe üst sınır yok (yalnız > 0). | Hatalı büyük bahşiş tavanı yükseltir |

Temiz: KDV formülü (100 TL -> 9,09; 9,99 TL -> 0,91) dokümanla birebir; decimal ve AwayFromZero; Billing'de servis/kuver ekleyen HTTP ucu yok; bahşiş vergisiz, ayrı satır, UI alanı boş geliyor; ödenmiş hesap iptal edilemez, iptalde 1-500 karakter sebep ve denetim olayı; iade ikinci kişi onaylı; ikram vergi 0; fişte kişisel veri yok; fiş "mali fiş değildir" etiketli.

Doğrulanamayanlar: fişte zorunlu alan listesi repoda yok; bilgi fişinin gerçek kullanımı mali müşavir teyidi ister; satış belgesi saklama süresi yazılı değil; 2026-01-30 yasak metni yalnız V1-WTR-020'de; KDV oranlarının mevzuat doğruluğu; M-1/M-6 elle hesap (test koşulmadı).
Ek: PaymentTender.log'da PaymentTenderHttpTests.cs:709 kırmızı görüldü (test-kalitesi ajanına devredildi).
