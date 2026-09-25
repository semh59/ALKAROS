# Kalan eksikler kaydı (2026-09-25)

Bu kayıt iki kaynağı tek yerde toplar: (1) 2026-09-25 iletişim ağı taraması, (2) daha önceki eksik analizinden
devreden maddeler. Her satırda kanıt düzeyi yazılıdır; "kodda doğrulandı" dışındakiler yeniden doğrulanmadan
görev sayılmamalıdır.

## 1. İletişim ağı taraması (bu oturumda yapıldı)

Yöntem: sunucu rotaları (`src/**/*.cs` içinde `MapGet/MapPost/...` ve `MapGroup`) ile istemci kaynaklarındaki
yol dizgeleri betikle çıkarılıp iki yönde karşılaştırıldı; hub olay adları, yeniden bağlanma davranışı, oturum
düşmesi ve yazıcı/mutfak hata yolları elle okundu. Hiçbir bulgu ağı gerçekten keserek denenmedi.

| Bulgu | Kanıt düzeyi | Görev |
| --- | --- | --- |
| Garson canlı bağlantısı kalıcı kapanıyor, açılışta başlatma hatası yutuluyor, yeniden bağlanınca telafi yok | kodda okundu | `V1-RMD-285` |
| Kasa yardım çağrısı bağlantısında aynı sorun | kodda okundu | `V1-RMD-286` |
| Garsondan kasaya hesap gönderimi 10 sn'lik sorgu ile görülüyor, itme yok | kodda okundu | `V1-RMD-287` |
| Outbox ölü mektupları yalnız günlüğe yazılıyor | kodda okundu | `V1-RMD-288` |
| Yardım çağrısı `Clients.All` ile herkese yayınlanıyor | kodda okundu | `V1-RMD-289` |
| Cashier ve WaiterPwa E2E paketleri CI'da yok | doğrulandı (iş akışı dosyası) | `V1-RMD-290` |
| PosTerminal ve statik Kasa sayfalarında oturum düşmesi tutarsız işleniyor | kodda okundu | `V1-RMD-291` |
| İndirim, bahşiş, düzeltme, özel hesap uç noktalarının istemcisi yok | betikle doğrulandı | `V1-RMD-292` |
| Yazdırma işleri ve denetim uç noktalarının istemcisi yok, yazıcı hatası personele görünmüyor | betikle doğrulandı | `V1-RMD-293` |
| Ödeme, nakit tahsilat ve indirimden sonra müşteri ekranına `SnapshotChanged` gitmiyor gibi | kodda okundu, yerelde doğrulanmadı | `V1-RMD-294` |
| Çevrimdışı yetki mutabakatı uç noktasının istemcisi yok | betikle doğrulandı | `V1-RMD-295` |
| Yaklaşık 95 yönetici uç noktasının istemcisi yok | betikle doğrulandı | `V1-RMD-296` |
| `LowStockAlertHub` sunucudan gönderiyor, dinleyen istemci yok | kodda okundu | `V1-RMD-296` içinde |

Sağlam bulunanlar: 5 outbox olay türünün hepsinin üreticisi ve tüketicisi var; hub olay adları istemcilerle
eşleşiyor; istemcilerin çağırdığı 98 yolun hepsinin sunucuda karşılığı var; sipariş mutfak bileti aynı işlemde
yazıldığı için mutfağa iletim hatası siparişi geri alıyor.

## 2. Önceki eksik analizinden devreden maddeler

Bu maddeler önceki oturumun derlemesinden alındı ve bu geçişte yeniden doğrulanmadı.

| Madde | Not |
| --- | --- |
| E2E ana planının 4–6. fazları | Faz 1–3 tamam; `V1-RMD-290` yalnız mevcut paketleri CI'a alır |
| Saklama süpürmesi üreticileri | Çalıştırıcı var, kayıt üreten veri görevleri yok (V14 bağımlı) |
| WAL gönderimi ve RPO | Şifreli dış yedek var; sürekli arşiv yok |
| Ödeme ile mali belge bağlantısı | `Paid` olan hesabın mali belgeye bağlanması |
| `PartiallyPaid` hesap durumu | Kısmi ödeme bugün ayrı bir durum değil |
| Bekleme süresi uyarısı | Kasada uzun süre bekleyen hesap için |
| Geri çağırmada birleştirme | Geri çağrılan hesap yeni taslakla birleşemiyor |
| Kalem bazlı bölme | Tutar bazlı bölme var |
| PosTerminal Masalar ekranında ham GUID | Kullanıcıya görünen Türkçe ad yerine kimlik |
| `IEftTenderHandler` kullanılmıyor | Kasıtlı mı bilinmiyor |
| NFC eşzamanlılık testi kararsız | Bilinen kararsızlık |
| Erişilebilirlik kapısı izin listesindeki 25 servis | `V1-RMD-278` ile bilerek bağlanmadı |
| v1.5 Faz 3 (Yemeksepeti), Faz 4, Faz 5 | `floofy-wishing-dewdrop.md` planı |
| Dış bağımlılıklar | Token/Beko, yemek kartı, QNB, Yemeksepeti, yazıcı, GİB, lisans |

## 3. Bu taramada bakılmayanlar

- Dış bağlantıların (Token/Beko, Cloudflare relay) kopma davranışı.
- Mutfak ekranının canlı bağlantısı.
- HTTP yöntemi (GET/POST) uyumsuzluğu: eşleştirme yalnız yol üzerinden yapıldı.
- Bulguların hiçbiri ağı kesen gerçek bir E2E ile denenmedi.
