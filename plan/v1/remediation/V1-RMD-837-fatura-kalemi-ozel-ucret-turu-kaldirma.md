# V1-RMD-837 - Hesap ayarı türü CustomFee: üretimde hiçbir yolu olmayan tür kaldırılıyor

- Task ID: V1-RMD-837
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi G11 grubu, kayıt F0047 (V1-BIL-003): "desteklenmeyen ücret türleri mümkün değildir" kabulüne rağmen `AdjustmentType.CustomFee` enum'da, veritabanı CHECK'inde (`bill_adjustments.adjustment_type`) ve herkese açık yapıcıda kabul ediliyor; hiçbir fabrika, uç nokta veya test onu üretmiyor (yalnız kasa ekranı etiket sözlükleri ve bir test verisi anıyor). Düzeltme: tür enum'dan, kasa ekranı sözlüklerinden ve veritabanı CHECK'inden kaldırılır (yeni migration 217).

Migration ileri yönde önce `CustomFee` satırı var mı bakar; varsa açık hata verir ve veriyi dönüştürmez. Geri yön CHECK'e değeri geri koyar.

## Owned surface

- `evidence/V1-RMD-837/**`
- `database/migrations/V1/V1-RMD-837/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Billing/Adjustments/AdjustmentEnums.cs (V1-BIL-003) — tür kaldırılır.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V13-PUI-004) — iki etiket sözlüğünden tür çıkar.
  - tests/Clients/StaticApps/split-payment-undo-adjustment.test.js (V1-RMD-830) — test verisinden tür çıkar.
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs (V1-RMD-698) — CHECK ve geri alma testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test (gerçek Postgres): her enum değeri CHECK tarafından kabul edilir, `CustomFee` reddedilir; `CustomFee` satırı varken migration açık hatayla durur; geri alma değeri geri koyar ve yeniden uygulanır.
2. Düzeltme, yeşil, mutasyon turu (`cp` + `cmp` ile geri alınır).
3. Migration listesi/kayıt dosyaları (`order.json`, `PhaseBMax`, manifest testi).
4. İngilizce kod ve test adları; ekrana basılan metin Türkçe kalır.

## Out of scope

- Yeni bir ücret türü eklemek; 218 numaralı bütünlük migration'ı (V1-RMD-838).

## Dependencies

- V1-RMD-830

## Acceptance evidence

1. Migration testleri, Manifest testleri, ilgili Billing modül testleri ve StaticApps tam paket yeşil; kırmızı ve yeşil çıktı `evidence/V1-RMD-837/` altında.
2. Mutasyon: CHECK'e tür geri konunca ve ön kontrol kaldırılınca testler kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
