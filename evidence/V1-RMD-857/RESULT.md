# V1-RMD-857 sonuç

Üretim kodu değişmedi; kusur çıkmadı.

## Test 1: üç SQL kopyası eşit (BillOrderLinkCopiesTests.cs, yeni)
Billing `BillOrderLink.Matches` kanonik sayılır. Tables `TableBillOrderLink` (iç sınıf) ve Reporting `PostgresBusinessDayTotalsReader.Sql` (iki satır içi kopya: iptal edilip iade edilmiş fatura, ödenmiş fatura) yansıtmayla okunur; takma adlar kanonik biçime çevrilip boşluklar sadeleştirilerek karşılaştırılır. Modüller birbirine başvuramadığı için tek kaynağa indirme yapılmadı.

## Test 2: kilit altı yeniden okuma yarışı (MergeOrdersHttpTests.cs)
İki test; ikisinde de test bağlantısı sipariş satırını commit etmeden tutar, birleştirme isteği test bağlantısının pid'ini bloklayan bir oturum (`pg_blocking_pids`) görünene kadar yoklanır (sabit bekleme yok; başka bir arka plan kilidi yanlış pozitif vermez), sonra commit edilir:
- `AnOrderCancelledOrMovedWhileTheMergeWaitsForItsLockIsNotBilled` (kuram): sipariş iptal -> `UNSUPPORTED_ORDER_STATE`, başka masaya taşıma -> `MERGE_DIFFERENT_TABLES`; fatura/kalem oluşmaz.
- `AnItemCancelledWhileTheMergeWaitsForTheLockIsLeftOffTheBill`: kalem iptal -> 409 `MERGE_ORDER_EMPTY` (kilit altı yeniden okuma).
Isınma birleştirmesi soğuk başlangıç yanlışını önler.

## Mutasyon (cp+cmp ile geri alındı)
T1 Tables OR->AND (3/5 test düştü), R1 Reporting koşul tersi (1/5), R2 Reporting OR->AND (1/5), B1 Billing koşul tersi (5/5): hepsi ÖLDÜ. L1 `FOR UPDATE OF o` kaldırıldı (3 test düştü), L2 durum denetimi kaldırıldı (1), L3 kilit altı yeniden okuma yerine önceki okuma kullanıldı (1), L4 kilit altı masa denetimi kaldırıldı (1): ÖLDÜ (mut-*.log).

## Koşular
Billing host: MergeOrdersHttpTests + MergedBillOrderLinkTests + BillOrderLinkCopiesTests tümü yeşil (green-final.log).

## Sınır
Eşitlik testi metin düzeyindedir (takma ad ve boşluk dışında birebir); anlamsal olarak eşdeğer ama farklı yazılmış bir koşulu ve büyük/küçük harf farkını yakalamaz, bilinçli seçim: kopyaların birebir kalması amaçlanır. Reporting dosyasındaki `billing.bill_items` geçiş sayısı 2 olarak sabitlenir; üçüncü bir satır içi kopya eklenirse test düşer.
- "Sipariş kayboldu" dalı (kilit altında satır yok) yarışla sınanmadı: siparişler silinmez, yalnız durum değiştirir. "Kasaya gönderilmiş karışık" mevcut testle kapsanır.
- Kilit beklerken siparişe kalem eklenmesi ayrıca sınanmadı (aynı yeniden okuma yolu, kalem iptali ile kanıtlı); aynı sipariş kümesi için iki eşzamanlı birleştirme mevcut `TwoTerminalsMergingTheSameOrdersTogetherLeaveExactlyOneBill` ile kapsanır.
