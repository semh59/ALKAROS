# Reviews

## qa-ux-tasarim
Metin dugme adlariyla uyumsuzdu (Iade ve iptal vs Hesabi iptal et), aria-describedby eksikti. Duzeltildi: metin "Hesabi al > Hesabi iptal et; odeme alindiysa once iade", id + aria-describedby eklendi. Test guncellendi.

## qa-uctan-uca-akis
Kasiyer yolu sunucuda dogru kapali. Yuksek: garson table-draft yolunda fatura kapisi yok (TableDraftService.cs:112,327); ayri gorev olarak raporlandi, bu gorevin sahip yuzeyi disinda. Orta/dusuk: ClearBillPointerAsync kalan faturayi yeniden secmiyor; StartOrder 409 mesaji Ingilizce (DualScreenStore.Orders.cs:133); kismi odemede metin iade der (duzeltildi).
