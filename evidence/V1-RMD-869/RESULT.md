# V1-RMD-869 sonuç

Birleştirmede kilit beklerken silinen siparişin "sipariş kayboldu" dalı sınanmıyordu. Yeni test: kilit tutan işlem siparişi siler, birleştirme bekler (pg_blocking_pids ile doğrulanır), onaydan sonra 404 NOT_FOUND, hesap ve hesap kalemi oluşmaz. Üretim kodu değişmedi.
Önemli bulgu: ilk sürüm testte mutant (satır sayısı kontrolü kapalı) sağ kaldı, çünkü kilit altında yeniden okumadaki "bulunamadı" dalı aynı 404'ü veriyordu. Kontrol fazlalık değil: diğer sipariş kasaya gönderilmişse silinme MERGE_MIXED_SENT diye görünürdü. Test ikinci siparişi kasaya gönderilmiş seederek bunu da sınar; mutant ölür (mut-1.log).
Testler: Host Billing MergeOrdersHttpTests 15/15 (gerçek Postgres 55432).
