# V1-RMD-791 - Sonuç

## Değişiklik (gün sonu kapanışı, sunucu tarafı)
- Kapanış istemciden sayı almaz: istek yalnız `acknowledgeOpenItems` taşır. İptal sayısı pencere içi `Order.ItemVoided` + `Order.SentItemVoided` denetim olaylarından, yazıcı hatası pencere içi `failed_at` yazılmış baskı işlerinden (sonradan basılsa da hata, "kurtarılan" = hata sonrası `Printed`) hesaplanır.
- Garson özeti (`serving_user_id`), yazdırma özeti (`printers.station_id`), masa kırılımı (`table_id`) ve kasa mutabakat toplamları (pencerede kapanan oturum sayısı, beklenen, sayılan, fark, yönetici onaylı kapanış, kendi mutabakatı) sunucuda hesaplanıp kapanışla saklanır.
- Bahşiş ayrı satır (`total_tips_amount`): pencerede kapanmış (`Paid`) hesapların `Tip` düzeltmeleri; ciro bahşişsizdir; iadeyle kapanan (`Cancelled`) hesabın bahşişi sayılmaz.
- Ön koşullar sunucuda: açık kasa oturumu, açık hesap, sonucu belirsiz ödeme. Engel yok; onaysız kapanış 409 `BUSINESS_DAY_OPEN_ITEMS_NOT_ACKNOWLEDGED`, onaylıysa sayılar + bayrak güne ve `reporting.business_day_closed` denetim olayına aynı işlemde yazılır. `GET .../{tarih}/close-preflight` aynı sayıları kapanıştan önce gösterir.
- Tek iş günü penceresi `BusinessDayWindow` (06:00-06:00): toplam okuyucusu, ödeme mutabakat raporu ve kanal raporu bunu kullanır (kanal raporu önceden gece yarısı kesiyordu; test verisi sınırları buna göre güncellendi).
- F0729: tüm sayılar tek SQL deyimiyle (tek anlık görüntü), kapanış işleminin içinde, günün kilidi ve `FOR UPDATE` altında okunur; önizleme ile kapanış arasında değişen sayı onaysız geçmez.
- Göç 214 (yedi sütun), `order.json`, `MigrationManifest`, `ManifestTests`, V1Operations test projesinin elle seçilmiş listesi. Belge: `money-tax-business-date.md` ek bölüm (06:00 operasyonel gün ile `service_date` ayrımı, bahşiş kuralı; onaylı satırlara dokunulmadı), `module-dependency-rules.md` + `ModuleBoundaryTests` (Host.Experience.Reporting -> Audit kenarı). `IBusinessDayTotalsReader` özeti iade dışlamasını anlatır (F0422).

## Mevzuat notu
Bahşiş gönüldür, KDV matrahına eklenmez. Kartla bahşişin yasal/muhasebe işlenişi mali müşavirce TEYİT EDİLMEMİŞTİR; kod yalnız ayrı alan sağlar, muhasebe iddiası yoktur.

## Doğrulama (gerçek Postgres)
- Yeni testler: `EndOfDayServerFiguresTests` 19 (iptal, yazıcı, bahşiş, iade+bahşiş, garson/masa, kasa, başlangıç dahil/bitiş hariç, ön koşul tanımları, önizlemeden sonra açılan kalem hâlâ 409, onaylı kapanış + audit alanları, önizleme = kapanış, kapanmış gün önizlemesi 409, izinler, çift kapanış, gövdesiz kapanış), `BusinessDayWindowTests` 7, V1Operations deposu 4 yeni test + göç 214 ileri/geri, kanal raporu 06:00 testi.
- Sonuçlar: Host Reporting 30/30, V1Operations 15/15, Channels 10/10, Payments 12/12, Host.Errors 224, Manifest 18, ApiConventions 17, ModuleBoundaries 17; atlanan 0.
- Kırmızı-önce: sözleşme değişikliği (istekten sayıların kalkması, yeni imzalar) eski kodla derlenmediği için klasik kırmızı koşu yoktur; her kuralın testi mutasyonla kırılır (aşağıda).
- Mutasyon (`mutation-summary.txt`): 17 mutant (m14-m17 sınırlar, kanal etiketi, Closing durumu) (pencere başlangıcı, SentItemVoided, bahşiş durumu, ciro-bahşiş, yazıcı penceresi, kurtarılan, onay denetimi, audit adı, onay bayrağı, kasa penceresi, açık hesap, belirsiz ödeme, işlem içi audit); hepsi öldü; m11 ilk turda yaşadı, bahşiş testine açık-hesap sayısı doğrulaması eklenip öldü; her turda `cmp` ile birebir geri alındı.

## İncelemeler
- qa-backend (10 bulgu), qa-test-kalitesi (18): giderilenler: kanal raporunun satır etiketi de 06:00 kaymasıyla (pencere değişmişti, günlük gruplama gece yarısında kalmıştı; yeni test kırmızıydı, düzeltildi); kapanmış günün önizlemesi 409; gövdesiz kapanış; kullanılmayan `RecordWaiterSummaryAsync`/`RecordPrintErrorSummaryAsync` silindi; testsiz `ReportingDomainTests` sınıfı silindi; test boşlukları (sınırlar, iade+bahşiş, audit alanları, açık kalem tanımları, önizlemeden sonra açılan kalem, null garson/masa, yalnız iptali olan garson, yazıcı retry, izinler, çift kapanış hata kodu) testlendi.
- Bilinçli davranışlar (hata değil): pencere bitmeden (örn. 02:00) kapanış mümkündür, kapanıştan sonraki hareket hiçbir günde sayılmaz, bu yüzden kapanış sayıları kapanış anının görüntüsüdür; çok günlü iadede brüt ödeme ödeme gününde, iade iade gününde görünür ve bahşiş ödenen günde kalır; sahibi/masası boş sipariş garson/masa kırılımında yer almaz ama genel toplamdadır; `kitchen.print_jobs.printer_id` FK `RESTRICT` olduğundan yazıcısız iş olamaz. Ürün marj raporu (`ProductMargin`) takvim günü ve fatura açılış tarihiyle kendi tanımını korur (kendi testi gece yarısı sınırını doğrular); operasyonel gün raporu değildir.

## Açık risk / kapsam dışı
- Yönetim "Gün Sonu" ekranı hâlâ eski istek biçimini gönderir (sunucu fazla alanları yok sayar); açık kalem varsa 409 alır. Ekran V1-RMD-792'de.
- Açık oturum/hesap/ödeme sayıları işletme geneli (anlık durum); pencereye bağlı değildir.
