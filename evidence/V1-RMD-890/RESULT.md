# V1-RMD-890 sonucu

`MergedBillOrderLinkTests.TheTillQueueShowsTheMergedBillOnItsOrdersAndDropsThemOncePaid` kırmızıydı (red-1.log: "Expected 2, Actual 1"). Kök neden bayat beklenti: V1-RMD-871 (5abae46e) birleşik hesabı kasa kuyruğunda tek çek yaptı (en eski sipariş temsilci, kalem sayısı ve toplam birleşir), test hâlâ iki satır bekliyordu. Üretim kodu doğru, değişmedi.
- Test artık tek satırı, temsilci siparişi (ilk sipariş), kalem sayısı 2, toplam 200 ve önizleme "Dish, Dish" değerini sınar; ödenince iki sipariş de kuyruktan çıkar.
- Kanıt: green-2.log ve final-2.log (14/14, exit 0). Mutasyon (mutations.txt): Q1 gruplama yok, Q2 kalem sayısı toplanmıyor, Q3 son sipariş temsilci, Q4 toplam toplanmıyor, Q5 önizleme birleşmiyor; hepsi öldü; üretim dosyası cp+cmp ile geri alındı ve yeniden derlendi.

## Açık risk
- "En eski sipariş" `created_at, order_id` sırasına dayanır; iki sipariş aynı zaman damgasını alırsa temsilci `order_id` ile belirlenir ve temsilci doğrulaması kırılgan olabilir (seed iki ayrı kayıtta zaman damgası farklı).
- "Ödenince" doğrulaması bill durumunu doğrudan SQL ile Paid yapar, gerçek ödeme akışından geçmez.
