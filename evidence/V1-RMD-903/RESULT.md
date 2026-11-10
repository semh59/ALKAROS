# V1-RMD-903 sonucu

Geri alınan indirim/ikram hesap toplam sınırından düşüyor; garsonun kendi verdiği kalem ikramı hesabın toplamına giriyor.

## Değişiklik
- Geri alma: 902'de toplam kaydedilmiş düzeltmelerden okunduğu için silinen (geri alınan) düzeltme kendiliğinden düşer; bu görev bunu yüzde indirim üzerinde gerçek uç noktalarla kanıtlar (902 ikram üzerinde kanıtlamıştı).
- Kalem ikramı: BillAppliedAdjustmentTotals toplama billing.bill_items'ta line_type = 'Complimentary' olan kalemlerin discount_amount'unu (ikram edilen kalemin tam değeri) ekliyor. Satır indirimleri (ikram olmayan) sayılmıyor.

## Kanıt
- BillAdjustmentTotalCapUndoHttpTests 4/4 gerçek Postgres (green-1.log): indirim geri alınınca kasiyer yeniden ikram edebilir; kalemi ikramlı hesapta 110 TL ikram yönetici onayına düşer, temiz hesapta uygulanır; ikram olmayan kalem indirimi sayılmaz; kalem ikramı tam değeriyle sayılır (sınıra tam ulaşma geçer, bir kuruş aşım yönetici onayına düşer).
- Komşu: BillAdjustmentTotalCap* sınıfları final-cap.log.
- Mutantlar (mutations.txt): 5 öldü (kalem ikramı sayılmıyor [red-1.log], her kalem indirimi sayılıyor, hesap filtresi yok, geri alınan düzeltme silinmiyor, kalem değeri 10 eksik sayılıyor).

## Not
Kalem ikramının kendi kişisel bütçe tavanı değişmedi; kalem ikramı kapıdan geçmez (yalnız hesap düzeyi istekler sınırlanır), sadece sonraki hesap düzeyi isteklerin toplamına girer.

## Açık (tur 1 bulguları)
- Hesap açıkken garsonun yaptığı kalem ikramı sipariş kalemini Complimentary yapıyor ama hesap kalemi (bill_items) Sale kalıyor (Order.ItemComplimentary'yi dinleyen kod yok): bu durumda toplam o ikramı görmez ve misafir kalemi hâlâ öder gibi görünür. Hesap, ikramdan sonra oluşturulduysa (BillItem.FromOrderItem) doğru taşınır ve sayılır. Bu, 903'ten önce de var olan ayrı bir şüphedir; V1-RMD-905'te gerçek uç noktayla yoklanır.
- Kalem ikramı hesap düzeyi kapıdan geçmez (SubjectType OrderItem); yalnız toplama girer. Kalem ikramının sınırı aşınca yöneticiye düşmesi bir ürün kararıdır.
- Kalem ikramı geri alınamıyor (uç nokta yok), bu yüzden "geri alınan kalem ikramı" durumu oluşmaz.
- Testteki kalem ikramı SQL ile kurulur (BillItem.FromOrderItem'ın yazdığı temsil: line_type Complimentary, discount_amount = gerçek değer, gross/net/tax 0); gerçek akış 905'te.
