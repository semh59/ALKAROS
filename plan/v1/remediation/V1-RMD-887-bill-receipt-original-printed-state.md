# V1-RMD-887 - Fiş "orijinal basıldı" durumu sunucuda tutulur (ilk basım orijinal, sonrakiler kayıtlı KOPYA)

- Task ID: V1-RMD-887
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih, 2026-11-07): bir hesabın bilgi fişinin ilk basımı orijinaldir, sonrakiler her yoldan KOPYA'dır ve kayıtlıdır; eski `GET .../receipt` ucu da bu duruma bağlanır. Bugün orijinal/kopya bilgisini yalnız kasiyer sayfasının bellekteki durumu tutuyor: sayfa yenilenince ya da başka terminalden açılınca ilk basım "orijinal" sayılmıyor, `GET .../receipt` ise her çağrıda kayıtsız veri veriyor, yani orijinal fiş sınırsız kez basılabiliyor.

Çözüm: hesap satırına ilk basımın zamanı ve basan kullanıcı (`billing.bills.receipt_first_printed_at/_by`) yazılır. Yeni `POST .../receipt/print` (ödenmiş hesap, `payments.take`) tek işlemde ilk basımı atomik olarak talep eder (`UPDATE ... WHERE receipt_first_printed_at IS NULL`): kazanan çağrı orijinaldir (`isCopy=false`, denetim `bill.receipt.printed`), diğer her çağrı kopyadır (`isCopy=true`, denetim `bill.receipt.reprinted`). Denetim kaydı aynı işlemde yazılır; fiş verisi bundan sonra okunur. Mevcut `POST .../receipt/reprint` bu uçla yer değiştirir (kaldırılır). `GET .../receipt` salt okunur kalır (iade ekranı kalem verisini buradan okur) ama yanıtı `originalPrintedAt` ve `isCopy` taşır; orijinal basıldıktan sonra GET yanıtından fiş çizen her istemci KOPYA yazar. Kasiyer sayfası her basımda yeni uca gider ve KOPYA'yı sunucudan gelen `isCopy` ile çizer; bellekteki `originalPrinted` durumu kalkar.

## Owned surface

- `evidence/V1-RMD-887/**`
- `database/migrations/V1/V1-RMD-887/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/MigrationComposition/order.json — 223 kaydı
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax 223
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 223 kaydı
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs — print ucu, GET alanları
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — her basım print ucundan
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - tests/Host/Experience/PaymentTender/BillTerminalAccessTenderHttpTests.cs — rota listesi
  - tests/Architecture/ApiConventions/idempotency_allowlist.json — reprint yerine print
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Migration (up/down) ve manifest kayıtları
2. Atomik orijinal talebi, kopya kaydı, denetim olayları, GET alanları
3. Kasiyer sayfasının sunucu durumuna bağlanması
4. Gerçek Postgres testleri (ilk basım orijinal, ikinci kopya, eşzamanlı iki basımdan yalnız biri orijinal, GET durumu, ödenmemiş hesap, yetki), mutasyon, iki bağımsız qa incelemesi

## Out of scope

- Mali fiş / e-Arşiv (bu belge mali olmayan bilgi fişidir); basımın fiziksel yazıcıya gittiğinin doğrulanması (tarayıcı yazdırma penceresi açılır, başarılı basım garanti edilemez)
- Orijinal durumunu sıfırlayan yönetici işlemi

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Migration ileri/geri kanıtı
3. Mutasyon sonuçları
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
