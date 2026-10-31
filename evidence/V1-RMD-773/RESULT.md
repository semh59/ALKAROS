# V1-RMD-773 sonuç

Bulgu (V1-RMD-719, B011 sunucu yarısı) HEAD'de DOĞRULANDI ve düzeltildi. Baseline: e5c88a81.

## Kırmızı kanıt (düzeltmeden önce)
- red-tabledraft: Draft sipariş send-to-cashier -> 200 OK (beklenen 409); masa Cleaning olur.
- red-billing-foundation: Active + Draft kalemli siparişten Bill.FromOrder 2 kalem alıyor (beklenen 1).
- red2-draft-order-bill: Draft siparişten from-order -> 200 OK (beklenen 409); kalemsiz fatura oluşuyor.
  (green2-billing-host: bir tohum Draft siparişti, kural eklenince kırıldı; tohum Submitted yapıldı, green3 yeşil.)

## Uygulanan kurallar
1. CashierHandoffStore.SendCheckToCashierAsync: sipariş durumu Draft ise CheckNotYetOrderedException -> 409 ORDER_NOT_SUBMITTED (Türkçe mesaj). Aynı transaction, tablo/sipariş değişmez, duyuru yok. Durum yalnız ileri gider (Draft -> Submitted), bu yüzden satır kilidi gerekmedi.
2. Bill.FromOrder: Draft kalemler faturaya girmez (üretimdeki tek çağıran BillingSplitStore).
3. BillingSplitStore.CreateBillFromOrderAsync: Draft sipariş için mevcut UNSUPPORTED_ORDER_STATE 409. (Backend incelemesi: bu olmadan Draft siparişte kalemsiz 0 TL fatura oluşuyordu.)

Garson istemcisinin mevcut kuralı (bill.js: düğme yalnız Active kalemi olan, yerel gönderilmemiş turu olmayan hesapta görünür) ile tutarlı; yeni iş kuralı icat edilmedi. Kural kalem düzeyinde değil sipariş durumu düzeyindedir: Submitted siparişteki bekleyen Draft kalem (ör. tutulan kurs) kasaya göndermeyi engellemez, yalnız faturaya girmez.

## Yeşil kanıt (hepsi exit=0)
BillFoundation 50, TableDraft 107, Billing Host 113, Tables Host 39, Billing Adjustments 21, SplitDesign 40, PaymentClosure 24, Orders Confirmation 38 / Comp 21 / Void 5 / VoidSent 24, ApiConventions 5 (yeni hata kodu mevcut kuralları bozmadı).

## Mutasyon (mutations.log)
- mut1: Bill.cs Draft dışlaması kaldırıldı -> test KILLED.
- mut2: CashierHandoffStore Draft kontrolü etkisizleştirildi -> test KILLED.
- mut3: BillingSplitStore Draft koruması kaldırıldı -> test KILLED.
Her biri cp + cmp ile geri alındı (restored1/2/3).

## Mevcut veri ve eski sipariş etkisi
- Gerçek üretim verisine erişim YOK. Yalnız yerel geliştirme veritabanı (docker alkaros-postgres-1, db alkaros) var: 3 sipariş, 3'ü Draft, 0 fatura; şeması eski (table_sent_checks tablosu yok). Bu veri üretimi temsil etmez.
- Geriye dönük düzeltme YAPILMADI. Tespit sorguları: legacy-detection.sql (Draft siparişe ait table_sent_checks satırı = masadan ayrılmış ama kuyrukta hiç görünmeyen sipariş; Draft kalemi olan fatura kalemi = müşteriden mutfağın hiç yapmadığı ürün için tahsil riski).
- Eski faturalarda Draft kalem varsa düzeltme/iade bir PARA kararıdır; Semih'e bırakıldı.

## Açık riskler (kapsam dışı)
- Kasa kuyruğu tutarı (orders.total) Draft kalemleri sayar (OrderItem.CountsInOrderTotals); Submitted siparişte bekleyen Draft kalem varsa kuyruk tutarı fatura tutarından yüksek görünür.
- Fatura açıldıktan sonra ateşlenen kalemler mevcut faturaya eklenmez (eskiden de eklenmiyordu; önceden Draft kalem baştan dahildi).
- DualScreenStore.QrBill.cs:67 canlı hesap satırlarında Draft kalemi de sayar (müşteri ekranı); fatura ile fark edebilir.
- BillSourceOperations (CreateMergedBill/CreateSplitBills) Draft'ı hâlâ dışlamaz; üretimde çağıranı yok.

## İncelemeler
- qa-backend: kural 1 doğru/yarışsız; bulgu 1 (Draft siparişte boş fatura) -> kural 3 ile giderildi; bulgu 2 eski davranışla aynı (yukarıda risk).
- qa-test-kalitesi: üç kural mutasyonla kanıtlı; boşluklar: karma sipariş HTTP uçtan uca yok (tek çağıran, modül düzeyinde kanıtlı), kalem düzeyi kasaya gönderme kuralı bilinçli olarak yok.
