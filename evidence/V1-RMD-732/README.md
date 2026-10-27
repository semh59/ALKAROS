# V1-RMD-732 - Billing migration ve veri bütünlüğü (603 B021, B022, B031)

**Sonuç:** Hesap tablosuna ilk kez tutar kuralı ve iptal eden kullanıcı bağlantısı geldi, gereksiz indeks kalktı; geri alma (down) betikleri 032/075/108/123/180/209 için gerçekten çalıştırılıp tekrar uygulanıyor; masa işaretçisi yazılamazsa artık uyarı günlüğe düşüyor. Eski migration dosyalarına dokunulmadı (bayt hash); düzeltmeler yeni migration 209 ile.

## Bulgu bulgu
- **B021 (kısmen doğru):** `BillingSplitStore.CreateBillFromOrderAsync` işaretçi yazım hatasını sessizce yutuyordu; yutma bilinçli (önbellek, masa onarımı düzeltir) ama günlük yoktu → `Warning` günlüğü eklendi, test tetikleyiciyle yazımı bozup günlüğü ve yine de oluşan hesabı doğruluyor. "Ayrı bağlantıda yazım" mimari tercih olarak bırakıldı; Tables/Catalog/Identity FK dokümantasyonu bu görevde yazılmadı.
- **B022 (kısmen doğru):**
  - 180 down'ı çakışan `bill_number` varken çöküyor: bilinen ve betikte yazılı (elle temizlik); dosya değiştirilemediği için kapatılamaz, **kabul edilen risk**. Temiz veride geri alma artık test ediliyor.
  - 036/108/123 korumasız `ADD CONSTRAINT`: eski dosyalar değiştirilemez; tekrar uygulama zaten migration sırasıyla korunuyor. Yeni migrationlar koruma içeriyor (209: `IF NOT EXISTS`).
  - Down testleri: 032, 075, 108, 123, 180, 209 için "uygula → geri al → tekrar uygula" kuramı eklendi, hepsi geçiyor; 032/108 fixture'larda yoktu, bu test onları gerçek tam sıra ile sınıyor.
  - `order.json` 123 girdisine eksik tablolar (`payments`, `bills`) eklendi.
- **B031 (kısmen doğru):** `bills` durum CHECK'i zaten vardı (iddia yanlış). Eklenenler (`NOT VALID`, eski satırlara dokunmaz, yeni yazımlara uygulanır): tutarlar negatif olamaz CHECK'i, `cancelled_by` → `identity.users` FK'si. `ix_bill_items_bill` `uq_bill_items_bill_item`'ın baş sütunuyla aynı olduğundan kaldırıldı. **Yapılmadı:** kalem silme CASCADE'i (silme akışı kullanım analizi gerektirir), allocation `row_version` her zaman 1 (okuyan yok), 'Ödeme iade edildi' düz yazısı (veri kaydı, kod sabiti).
- Yan etki: `ACancellationWhoseAuditRecordCannotBeWrittenIsNotKept` rastgele kullanıcı kimliğiyle iptal ediyordu; yeni FK onu önce yakaladı, test gerçek kullanıcıyla düzeltildi. `cancelled_by` yazan tek iki üretim yolu (iptal ve iade) oturumlu gerçek kullanıcıyla çalışır.

## Yüzey genişletmesi
`tests/Host/Experience/Billing/**` yüzeyde yoktu; ayrı plan commit'iyle eklendi (+ 736'ya kapsam notu).

## Kanıt (logs/)
Manifest 18/18, Migrations 23/23 (yeni 7: 6 down kuramı + 1 şema testi), Host Billing 84/84 (yeni 1), mutasyon 4/4 KILLED.
