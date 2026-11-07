# V1-RMD-871 sonuç

Tanım ve karar: birleşik hesap (birden çok siparişten, bills.order_id boş) (1) kasa kuyruğunda aynı hesap kimliğiyle iki satır oluyordu ve her satır tüm hesabın ödenen tutarını kendi sipariş toplamıyla gösteriyordu; (2) müşteri ekranına hiç bağlanmıyordu (BindBillOrderToDisplayAsync siparişsiz hesabı reddediyordu), oysa ekran özeti (CustomerDisplayPaymentProgress) siparişin hesaplarını BillOrderLink ile birleşik hesap dahil zaten topluyor.
Düzeltme: kuyruk tek hesap = tek satır (en eski sipariş temsilci, kalem sayısı/toplam/önizleme birleşik, ödenen tutar hesaba ait); müşteri ekranı birleşik hesabı en eski siparişine bağlar. Kalem içermeyen tezgah hesabı bağlanmaz (mevcut test).
Kırmızı: red-1.log (bağlama), red-2.log (kuyruk). Yeşil: DualScreen bağlama 8/8, MergeOrdersHttpTests 15/15, TableDraft 28/28 (green-*.log).
Mutasyon (cp+cmp geri alındı): gruplama yok, toplam toplanmıyor, kalem sayısı toplanmıyor, en yeni sipariş seçimi: dördü de öldü.
Sınır: kuyruk önizlemesi birleşik hesapta her siparişin ilk üç ürününü birleştirir (en çok 3 x sipariş sayısı); ekranda gösterimi elle denenmedi.
