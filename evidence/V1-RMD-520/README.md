# V1-RMD-520 kanıtı

Doğrulananlar (gerçek Host + Postgres HTTP testi, `PaymentRefundHttpTests`):
- Bütçe içindeki nakit iade kasiyer tarafından hemen tamamlanır: kasadan çıkış, ödeme ters kaydı, hesap iptali, denetim kaydı (`viaAllowance`), bütçe düşer.
- Bütçeyi aşan iade `Requested` kalır, bütçe düşmez; müdür onayı bütçeden düşmez (`approval_mode` Manager).
- Bütçesi olmayan kasiyerde akış eskisi gibidir.
- Kartlı elle iade bütçeyi ayırır, kasiyer fiş numarasıyla tamamlar.
- Yalnızca isteyenin açık oturumu bütçe harcar.
- Bütçe ucu: yalnız `payments.refund.approve`; kendine verilemez (403); kullanılandan az olamaz (400); açık oturum yoksa 409; üst sınır 10.000.000.
- Billing test projesi 56/56, ApiConventions 5/5, ModuleBoundaries 9/9, manifest 16/16.

Doğrulanmayanlar: gerçek tarayıcı/arayüz yok (V1-RMD-521). Bilinen sınır: ayrılıp tamamlanmayan kartlı iade tutarı vardiya sonuna kadar geri gelmez.
