# V1-RMD-549 kanıtı

- `TelemetryPrivacyGuard`: izin listesi dışındaki alanlar atılır; e-posta, telefon, TC kimlik (sağlama toplamlı), IBAN ve kart numarası (Luhn) içeren değerler `[gizlendi]` ile değiştirilir; değer 200 karakterle sınırlanır.
- `TelemetryRetention`: ham olay 30 gün, saatlik özet 395 gün (13 ay), hata olayı 90 gün; süre aşan satırlar sınırlı partilerle silinir; güvensiz tablo/sütun adı SQL'den önce reddedilir; ham tablolar `raw_` önekiyle tanınır.
- Test: 18/18 gerçek Postgres (12 gizlilik örneği dahil, `test-runs.log`); mutasyon 10/10 öldü (`mutations.log`); proje manifesti VALID; tutarlılık denetimi temiz.
- Bu görevde yapılmayanlar: silme işinin zamanlanması ve denetim kaydı ile ham tabloların yedekten çıkarılması, tablolar V1-RMD-533/541 ile oluşturulduğunda orada bağlanır (şimdilik tablo yok; `IsRawTable` kuralı hazır). Bu iki madde V1-RMD-549'un plan kabul koşullarından "yedek dosyasında yer almaz" ve "denetim kaydı" kısmıdır: **henüz karşılanmadı**, 533 ve 541 görev kayıtlarına devredildi.
- Doğrulanmadı: KVKK uyumu için hukuki görüş (V1-RMD-550'de açık madde).
