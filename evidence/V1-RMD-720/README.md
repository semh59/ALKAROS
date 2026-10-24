# V1-RMD-720 - Orders test boşlukları ve TableDraft test düzeneği

**Sonuç:** B003 kapandı: TableDraft test projesi migration 205'i (ve ona gereken 024 birleştirme, 025 rezervasyon tablolarını) bağlıyor; 93 testin 10'u kırmızıydı, şimdi 94/94 yeşil. B032'nin hayatta kalan mutantları için testler eklendi; üretim kodu değişmedi.

- Yeni testler: `OrderMoneyAndCourseTests.cs` (indirim sınırı, yarım kuruş yuvarlama satır ve vergide, ikram toplamlarda kalır, iptal kalem toplamda yok, kurs sırası), `FiringACourseWhoseTicketIsAlreadyPrintedDoesNotPrintItAgain` (çift fiş), VoidSent: denetim yazılamayınca stok tüketilmiş kalır; aynı iptal tekrarında stok bir kez geri döner (denetim reddi yardımcıya çıkarıldı).
- Mutasyon (`mutations.log`): 7/7 KILLED, kaynak her seferinde geri yüklendi.
- Denk mutant: t2_stockall (`OrderSubmissionStockDispatcher` `IsActive` süzgeci) — kodun kendi yorumu bunu "kayış-askı koruması" diye belirtir; ateşlenen kalemler yapı gereği Active olduğundan davranış gözlemlenemez; test yazılmadı.
- Yapılmayan: mutfak/hesap adımı hata yolu testi ve gönderilmiş kalem iptalinde onay+yeniden deneme testi, bu adımların kendisi V1-RMD-722'de değişeceği için orada yazılır.
- Kanıt: `logs/` (tabledraft 94/94, orderaggregate 145/145, voidsent 21/21).
