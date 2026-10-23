# V1-RMD-706 Katalog yazmaları için denetim kaydı

Kapanan bulgu: V1-RMD-600-B008.

- `CatalogManagerEndpointFilter`: GET olmayan ve 2xx dönen her katalog isteğinden sonra `audit.audit_events` kaydı eklenir: olay adı `catalog.<kaynak>.<eylem>` (ör. catalog.products.create, catalog.products.availability), aggregate tipi `catalog.<kaynak>`, aktör yönetici kimliği, sonuç kaydı (after_state_json), istek izleme kimliği.
- Reddedilen (400/409) ve bulunamayan (404) istekler ile okumalar kayıt bırakmaz.
- Testler: Host.Experience.Catalog 23/23, Architecture.ModuleBoundaries 11/11, ApiConventions 5/5 (logs/). Test projesine 015 denetim göçü eklendi; Catalog Host'un Audit bağımlılığı mimari test ve belgede bildirildi.
- Mutasyon: 6/6 KILLED (mutations.log). İlk koşuda A2 ve A6 SURVIVED çıktı; 404 kaydı ve aggregate tipi için kontrol eklenip yeniden koşuda KILLED.

Sınır: yazma işlemi denetim kaydından önce işlenir (depolar işlem almıyor); denetim eklenemezse istek 500 döner ve eksik kayıt görünür olur. Fiyat süpürmesi ve ProductCatalogChanged olayı kapsam dışı. Gerçek tarayıcıda/ortamda doğrulanamadı.
