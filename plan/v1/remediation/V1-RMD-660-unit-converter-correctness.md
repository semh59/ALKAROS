# V1-RMD-660 - Birim çevirici doğruluğu (aynı boyutlu özel katsayı, Convert/TryConvert tutarlılığı, yükleme yeniden denemesi)

- Task ID: V1-RMD-660
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-596 bulguları B001, B004 ve B014 kapanır. B001: `koli`, `adet` gibi aynı boyutlu (Count) iki birim için kaydedilen özel katsayı stok, mal kabul, üretim ve reçete maliyetinde yok sayılıyordu (1 koli = 1 adet çıkıyordu); özel katsayı artık standart tablodan önce uygulanır, fiziksel boyutlarda (kütle, hacim) standart tabloyla çelişen kayıt reddedilir. B004: `CanConvert`/`TryConvert` ile `Convert` aynı kuralı izler (kayıtsız birim + özel kural). B014: açılışta veritabanı kesintisi olursa özel dönüşümler yeniden başlatmaya kadar kaybolmasın; yükleyici arka planda yeniden dener. Eski yorumlar koda uydurulur.

## Owned surface

- `plan/v1/remediation/V1-RMD-660-unit-converter-correctness.md`
- `evidence/V1-RMD-660/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Measurements/UnitConverter.cs - yalnız dönüşüm çözümleme ve kayıt kuralı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Measurements/IUnitConverter.cs - yalnız yorum
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Recipes/UnitConversionLoaderHostedService.cs - yalnız yeniden deneme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/BuildingBlocks/Measurements/DimensionSafeUnitTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Recipes/UnitConversionLoaderRetryTests.cs - yalnız yeni test dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-659-host-orchestration-edge-coverage.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Özel katsayı önceliği, fiziksel boyut çelişki reddi, Convert/TryConvert tutarlılığı, yükleyici yeniden denemesi ve bunların testleri.

## Out of scope

- Ekranda katsayı önizlemesi (Fırsat 596-F002), yazma işleminin bellek-veritabanı sırası (kabul edilen kalıntı: veritabanı yazımı başarısızsa kural yeniden başlatmaya kadar bellekte kalır; aynı çift yeniden gönderilince düzelir).

## Dependencies

- V1-RMD-659

## Acceptance evidence

1. Önce kırmızı test (koli→adet 12 Convert ve TryConvert, tersi, kayıtsız birim, kg→g 5 reddi, yükleyici yeniden denemesi), sonra yeşil; mutasyon kanıtı.
2. `evidence/V1-RMD-660/` altında çıktılar; Measurements, Recipes Host ve ilgili Inventory/Production/Purchasing testleri yeşil.

## Handoff

- V1-RMD-661
