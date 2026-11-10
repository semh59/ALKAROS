# V1-RMD-902 sonucu

Doğrudan (yetki motoruna girmeden) uygulanan ikram/indirim de hesap toplam sınırına (billing.adjustment_total_cap) sayılıyor; sınırı aşan doğrudan uygulama engellenmiyor, denetim kaydına işaret düşüyor.

## Değişiklik
- Kapı (BillAdjustmentTotalGate) artık verilmiş hibeleri değil hesapta fiilen uygulanmış indirim ve ikram toplamını okuyor: IBillAdjustmentTotals / BillAppliedAdjustmentTotals (billing.bill_adjustments, is_deduction). Hibe toplama sorgusu (SumGrantedForSubjectAsync) kaldırıldı. Hibe yolu, yönetici onaylı ve doğrudan uygulananların hepsi tek toplamda; geri alınan (silinen) düzeltme kendiliğinden düşer (V1-RMD-903 bunu ayrıca kanıtlar).
- Toplam artık kaydedilmiş düzeltmelerden okunduğu için aynı hesabın ikram/indirim istekleri, kapıdan kaydetmeye kadar tek hesap kilidiyle (BillAdjustmentLock, pg_advisory_xact_lock) sıralanıyor; hibe kaydı eskiden bu sıralamayı sağlıyordu.
- Her uygulanan ikram/indirimin denetim kaydına `totalCapExceeded` yazılıyor (uygulama sonrası toplam sınırı aştıysa true); engelleme yok ("yönetici sınırı aştı" işareti).

## Kanıt
- Birim (gerçek Postgres hibe motoru, sahte toplam kaynağı): BillAdjustmentTotalGateTests 9/9; Identity.Authorization 257/257 (final-identity.log).
- HTTP (gerçek uç noktalar ve Postgres): BillAdjustmentTotalCapHttpTests 10/10 (gerçek Postgres; eski dört-ikram testi artık dört ayrı kasiyerle, davranışsal sıkılaştırma yanlış nedenle geçirmesin diye): supervisor doğrudan 120 ikram -> kasiyer 50 bekler; sınır aşan doğrudan ikram engellenmez ve ikinci kayıtta işaret true (ilkinde false); geri alınan ikram sınırı yeniden açar; 5 farklı kasiyerden eşzamanlı ikram: tam 3 uygulanır, 2 bekler.
- Mutantlar (mutations.txt): 10 öldü (kilit kaldırıldı, kilit uygulamadan önce bırakılıyor, işaret sınırda da yanıyor, kapı HTTP düzeyinde kapalı, toplam hesaba bakmıyor, deduction olmayanlar sayılıyor, işaret hiç yazılmıyor, sınır uygulamaya geçmiyor, kapı toplamı yok sayıyor, sınıra tam ulaşma yönetici onayına gidiyor).
- Kapsam: final-billing.log (BillingSplitHttpTests ve BillAdjustment sınıfları) 92/92.

## Not
Aynı kasiyerden art arda çok hibe, mevcut davranışsal sıkılaştırma kapısını (BehaviouralTighteningGate) da tetikler; bu yüzden eşzamanlılık testi farklı kasiyerlerle kuruldu.

## Açık (karar gereği / not)
- Hibe `Granted` olduktan sonra uygulama düşerse ya da yönetici onaylı bekleyen hibe henüz uygulanmadıysa, uygulanana kadar sınıra sayılmaz; sınır zaten "engellemez, işaretler" ilkesiyle çalışır. İşaret uygulama anındaki toplamla hesaplanır.
- Toplam `is_deduction` olan her düzeltmeyi sayar (şu an yalnız ikram ve indirim bu tipte).
- Kalem düzeyi ikramlar (garson) 903'te toplama girer.
