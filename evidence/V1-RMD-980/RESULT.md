# V1-RMD-980 sonuc

(b) Hesap acma (BillingSplitStore.CreateBillFromOrderAsync) artik siparis satirini FOR UPDATE ile kilitler ve siparisi kilit altinda yeniden okur; ayni anda eklenen mutfaksiz kalem ya hesaba girer ya da hesap acildigi icin reddedilir. Kirmizi: red.log (kalem faturadan dustu, 1 beklenen 2). Yesil: green.log.
(c) Yetersiz stokta mutfaksiz ekleme 409 ile reddedilir; kalem, stok ve denetim kaydi kalmaz (test yeni, davranis zaten dogruydu).
Regresyon: Billing projesi (BillOrderLinkCopiesTests haric) tablo-dali duzeltmesiyle 343 test gecti (billing2.log); masasiz dal ve durum kontrolu eklendikten sonra tam kosu bellek yetersizligi nedeniyle durduruldu, FromOrder/MergedBillOrderLink/CreateBill alt kumesi 14 test gecti (billing-from-order.log), KitchenlessAdd 24 test gecti.

Bilinen, bu gorev disi: BillOrderLinkCopiesTests.TheBusinessDayReportCopiesAreTheBillingCondition (2 veri satiri) master'da kirik: Reporting BusinessDayTotals SQL'inde billing.bill_items 3 kez geciyor, test 2 bekliyor (V1-RMD-935 sonrasi); 980'in degisikligiyle ilgisi yok.
Mutasyonlar: mutations.txt.
