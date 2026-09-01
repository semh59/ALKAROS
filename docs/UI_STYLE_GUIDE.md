# ALKAROS Arayüz Stil Rehberi

Bu rehber, ALKAROS istemcilerinde (PosTerminal, Cashier, WaiterPwa) kullanıcıya
görünen metinlerin dilini ve biçimini tek bir kurala bağlar. Amaç, farklı
oturumların ürettiği çeviri ve hata mesajı tutarsızlıklarını kalıcı olarak
önlemektir.

## 1. Dil sınırı

- Kullanıcıya görünen her metin Türkçedir: etiketler, başlıklar, düğme metinleri,
  durum rozetleri, hata mesajları, `aria-label` ve `title` değerleri, boş durum
  açıklamaları.
- Kod tarafı İngilizce kalır: sınıf, arayüz, metod, değişken, dosya adı, tip
  adı, log kaydı, exception mesajı, test adı ve kod yorumu.
- Bir kod tipi veya enum değeri (`KitchenUnknownDelivery`, `Healthy`) hiçbir
  zaman doğrudan ekrana yazılmaz; önce Türkçe karşılığına çevrilir.

## 2. Türkçe terim sözlüğü

| İngilizce kaynak | Türkçe karşılık |
| --- | --- |
| Catalog | Katalog |
| Unknown (yazıcı teslimatı bağlamı) | Doğrulanamayan |
| Unknown (genel kayıt bağlamı) | Bilinmeyen |
| Healthy | Sağlıklı |
| Degraded | Sınırlı |
| Unhealthy | Sorunlu |
| Failed | Başarısız |
| Completed | Tamamlandı |
| InProgress | Sürüyor |
| Queued | Bekliyor |
| Preparing | Hazırlanıyor |
| Ready | Hazır |
| Served | Servis edildi |
| Cancelled | İptal |

Yeni bir terim gerektiğinde önce bu tabloya eklenir, sonra kodda kullanılır.

## 3. Hata mesajı kuralı

- Kullanıcıya asla ham `error.message`, HTTP durum kodu metni veya sunucu
  gövdesi gösterilmez.
- Her hata dalı için önceden yazılmış Türkçe bir mesaj bulunur. Teknik ayrıntı
  yalnızca konsola veya log kaydına gider.
- Çakışma (409) durumları ayrı ve anlaşılır bir Türkçe mesajla ele alınır;
  kullanıcıya güncel veriyi alması söylenir.
- Bir alan sunucudan İngilizce enum değeri olarak geliyorsa, gösterimden önce
  bir çeviri haritasından (`Record<Enum, string>`) geçirilir; haritada karşılığı
  yoksa nötr bir Türkçe ifade (`Bilinmiyor`) kullanılır.

## 4. Periyodik İngilizce sızıntı taraması

Aşağıdaki tarama her kurtarma dalgasında ve sürüm kapısından önce çalıştırılır.
Kod tanımlayıcıları değil, yalnızca kullanıcıya görünen string literal'leri
hedefler.

```sh
grep -rniE '"[^"]*\b(Catalog|Unknown|Failed|Healthy|Degraded|Unhealthy|Pending|Draft)\b[^"]*"' \
  src/Clients/PosTerminal/src src/Clients/Cashier/wwwroot src/Clients/WaiterPwa/wwwroot \
  --include='*.tsx' --include='*.ts' --include='*.js' --include='*.html'
```

Çıkan her satır tek tek incelenir:

- Satır bir `aria-label`, görünen metin veya `title` ise, terim sözlüğüne göre
  Türkçeye çevrilir.
- Satır bir CSS sınıfı, veri anahtarı, API alan adı veya test beklentisi ise
  olduğu gibi bırakılır.

Bulunan gerçek sızıntılar, mevcut governance sürecine uygun olarak ayrı bir
kurtarma görevi altında düzeltilir; toplu ve sessiz bir refactor yapılmaz.
