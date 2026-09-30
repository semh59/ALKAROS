# V14-INV-003 - kabul kanıtı

Görev dosyasındaki kabul ölçütlerinin karşılandığını gösteren özet; komut çıktıları bu klasördedir.

- Gerçekleşen (gerçek PostgreSQL, tüm migration'lar; `evidence/V14-INV-003/tests.log`): 8/8. Bir hareket ödediği
  adisyonun KDV oranlarına bölündüğü için satır ile hareket çoktan çoğa eşleşir; `invoicing.invoice_line_sources`
  her (satır, hareket) çiftine katkı tutarını yazar. Bağlantılar tek işlemde yazılır ve işlem sonunda ertelenmiş
  kısıtlamayla tamamlığı denetlenir: her satır bağlantılarının toplamına, kaynak kümenin iptal edilmemiş her `Charge`
  hareketi tutarının tamamına eşittir. Yetim bağlantı (olmayan satır, başka kümenin ya da hiç seçilmemiş hareket),
  yinelenen bağlantı, eksik ya da hareketler arası kaydırılmış tutar veritabanında reddedilir; kayıtlı bağlantılar
  değiştirilemez ve silinemez (kim, ne zaman yazdı da tutulur). `RecordAsync` bölmeyi yeniden türetip satırlara karşı
  kanıtlar (uyuşmazlıkta hiçbir kayıt yazılmaz), yeniden deneme aynı izi döner, eşzamanlı dört çağrı tek iz yazar.
- Tamlık kısıtlamasını sıradan tetikleyiciye çevirince ilgili test kırmızı (`evidence/V14-INV-003/red-without-deferred-check.log`);
  migration 169 ileri/geri/ileri temiz (`evidence/V14-INV-003/migration-up-down.log`). Modül sınırı, bileşim/manifest
  (164/164), fatura oluşturma (21/21) paketleri ve tutarlılık denetimi yeşil.
- Kapsam notu: fatura oluşturma bu görevin dışındadır; `RecordAsync` çağrısı fatura oluşturmadan sonra V14-QNB-002
  gönderimden önce yapılmalıdır (`GetAsync` boş dönerse fatura izsizdir). Ödeme gibi faturalanmayan hareketler bağlanmaz.
