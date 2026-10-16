# V1-RMD-535 - Alan dedektörleri

`DomainDetectorRunner` (60 sn, izleme havuzu, salt okunur) mevcut durum tablolarını okur; koşul varsa hata olayı üretir, düzelince aynı kategorideki açık olayları kendisi çözer (parmak izi karşılaştırmasıyla). Okunamayan kaynak atlanır, o kaynağın olayları çözülmez, diğer kontroller sürer. Eşikler `Monitoring:DomainDetectors` bölümünden okunur.

Kapsam: yazıcı (başarısız iş, takılı kuyruk, bilinmeyen fiziksel baskı), ödeme (Bilinmeyen: 5 dk üstü Kritik, öncesi Uyarı; mutabakat gerekli), mutfak (kabul edilmeyen fiş), entegrasyon (sağlayıcı yoklama hatası, mağaza durumu ve katalog iletilememesi), yedek (son yedek başarısız, yedek eski, dış yedek RPO, geri yükleme denemesi başarısız).

Kanıt: `test-runs.log` (14/14 gerçek Postgres testi, DualScreenHostTests 6/6), `mutations.log` (12 mutant, hepsi öldü).

Doğrulanamadı / kapsam dışı kalanlar:
- QNB e-fatura: gönderim durumunu tutan bir tablo bulunamadı; dedektör yok.
- Disk: `resource.disk_high` zaten ResourceMonitor'da var; tekrarlanmadı. Kaynak modüller değişmediği için yazıcıda "çevrimdışı" yalnız iş kayıtlarından çıkarılır (yazıcı nabzı yok).
- "Siparişe fiş oluşmadı" (siparişi var, fişi yok) kontrolü yok; orders sütunları doğrulanmadan yazılmadı.
- Test şemasında `orders.orders` ve `billing.bills` yalnız anahtar sütunlu yedek tablolarla kuruldu.
- Docker yığınında enjeksiyon denenmedi (V1-RMD-540 kapsamı).
