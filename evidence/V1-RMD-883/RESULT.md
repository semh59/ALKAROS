# V1-RMD-883 sonucu

Cari yaşlandırma raporunda iki kusur kapatıldı.

## 1. Vade bilgisi
- Ledger satırlarında vade tarihi alanı YOK (`customer_account.account_transactions`: id, customer_id, transaction_type, direction, amount, source_reference_*, note, created_by, occurred_at). Vade yalnız müşteri başına var: `customer_account.credit_terms.payment_term_days` (1..365 veya NULL = vade kontrolü yok).
- Bu yüzden gün kovaları işlem tarihinden itibaren kalır ve başlıkları "İşlemden 0-30 gün" ... "İşlemden 90+ gün" yapıldı; açıklama metni bunu söyler.
- Yeni alanlar: API `paymentTermDays` ve `overdueAmount`, ekranda "Vade (gün)" ve "Vadesi geçmiş" sütunları. Vadesi geçmiş = müşterinin vade gününü aşmış açık borç (işletme günü yaşı > vade günü). Vadesi tanımlı olmayan müşteride ikisi de boş ("—"). Kredi politikasındaki gecikme kuralı anlık (şimdi - vade günü) ile hesaplanır; rapor işletme günü (06:00) sınırıyla hesaplar, fark yalnız gün sınırındaki saatlerde olabilir.

## 2. Veritabanında toplulaştırma
- FIFO kapatma pencere fonksiyonlarıyla veritabanında hesaplanır; bellek içi hesaplayıcı ve 500 bin satır sınırı kaldırıldı. 500.001 satırlık tek müşteri defteri artık doğru yaşlandırılır (ALedgerOfMoreThanHalfAMillionRowsIsAgedInsteadOfFailing).
- Yeni üst sınır müşteri sayısına konuldu (100.000, aşılırsa açık hata): sonuç tek yanıtta tüm müşterileri döndürüyor ve tüketici denetimi sınırsız SELECT'e izin vermiyor. Satır sınırı değil.
- Hesaplayıcı testleri gerçek Postgres servis testlerine taşındı (kova sınırları 29/30/31 dahil: 0/30/31/60/61/90/91 gün, saat 06:00 sınırı, parçalı ödeme, fazla ödeme, ters sıra, devreden kredi).
- Test düzeni: bağlantıyı zorlayan eşzamanlılık testiyle aynı xUnit koleksiyonuna alındı (yeni sınıf eklenince "too many clients" veriyordu).

## Davranış notu
- Rapor sonucu önceki hesaplayıcıyla aynı rakamları verir (taşınan testler aynı beklentilerle yeşil).
- N+1 profil okuması (uç noktada müşteri başına ad sorgusu) kapsam dışı bırakıldı.

## Kanıt
- Proje 45/45 (final-module.log, aging servis testleri 27), Host aging HTTP 6/6 (final-host.log), vitest ve tsc 0 (final-vitest.log, final-tsc.log).
- Mutasyon (mutations.txt): 13 mutant, hepsi öldü (müşteri sınırı +1, vade aralığı 1..364, sıfır yerine boş, kredi emilimi yok, baş çıkarımı yok, pay sınırsız, ilk kova katı sınır, fazla ödeme 0, vade sınırı kaydı, gün başlangıç saati, vade birleşimi yok, kova birleşimi). Her biri cp+cmp ile geri alınıp yeniden derlendi.

## İnceleme sonrası
- Vade günleri artık ayrı sorguyla okunmaz: 1..365 günün hepsi için kesim anı sorguya parametre geçilir (okuma ile sorgu arasında vade değişirse sessizce 0 çıkma riski kalktı).
- Vadesi olan müşteri yoksa "Vadesi geçmiş" toplamı "—" gösterir.
- Açık risk: 500 bin+ satırlık çok müşterili defterde sorgu planı ve süresi (EXPLAIN ANALYZE) ölçülmedi; `(customer_id, occurred_at)` indeksi `id`'yi içermediği için pencere sıralaması sıralama adımı gerektirir. Uç noktadaki müşteri başına profil okuması (N+1) kapsam dışı ve 100.000 müşteri sınırına yaklaşan raporda yavaşlatır.
- Doğrulama turu bulguları giderildi: vade üst sınırı `CustomerCreditTerms.MaxPaymentTermDays` sabitinden gelir; son kovanın etiketi "İşlemden 90 günden eski"; toplamda 0 ile boş ayrımı testli; vade birleşimi ve aralığı mutantları son kodda yeniden koşuldu.
