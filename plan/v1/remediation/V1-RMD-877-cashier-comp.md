# V1-RMD-877 - Kasada ikram: kasiyer ister, limit içinde kendisi uygular, üstü yönetici onayına gider

- Task ID: V1-RMD-877
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Ürün kararı F0223 (Semih onayladı): kasiyer ödeme ekranında bir hesabın bir kısmını veya kalanını ikram edebilir. Mevcut yetki motoru kullanılır (bills.comp izni, authorization_policies limiti, Yetki Kararları ekranı); paralel akış yazılmaz. Kapsam kararı: hesap düzeyinde tutar (kalem düzeyi ikram sipariş ekranında zaten vardır; kasa ekranı hesap kalemlerinin sipariş sürümünü bilmez). Varsayılan sınır (kasiyer, bills.comp): tek seferde 50 TL, 8 saatte en çok 5 ikram, üstü yönetici onayına gider; sınır Yönetim > Roller ve yetkiler > Onay sınırları ekranından değiştirilebilir (o ekran bills.comp/kasiyer satırını zaten destekler). Onay beklerken aynı idempotency anahtarı korunur; kasiyer 'Onay durumunu kontrol et' ile aynı isteği yeniden gönderir, onaylandıysa ikram uygulanır.

## Owned surface

- `evidence/V1-RMD-877/**`
- `database/migrations/V1/V1-RMD-877/222-cashier-comp-policy.down.sql`
- `database/migrations/V1/V1-RMD-877/222-cashier-comp-policy.up.sql`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/migrations/V1/V1-RMD-877/222-cashier-comp-policy.up.sql — kasiyer ikram limiti tohumu
  - database/migrations/V1/V1-RMD-877/222-cashier-comp-policy.down.sql — geri alma
  - tests/Clients/StaticApps/split-payment-comp.test.js — kasa ikram ekranı testleri
  - src/Host/Experience/Billing/BillingSplitApplication.cs — POST bills/{id}/comp ve ortak işleyici
  - src/Host/Experience/Billing/BillingSplitContracts.cs — ApplyBillCompRequestV1
  - src/Host/Experience/Billing/BillingSplitStore.cs — ikram gerekçesi doğrulaması
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — İkram formu
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax 222
  - database/MigrationComposition/order.json — 222 kaydı
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — manifest sayımı
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs — geri alma hedefi
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs — ikram uç nokta testleri
  - tests/Host/Experience/Roles/RoleManagementHttpTests.cs — tohumlanan kasiyer ikram satırı listede görünür
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. 222 tohum: kasiyer için bills.comp auto_within 50 TL / 5 adet / 8 saat, mevcut satıra dokunmaz, down yalnız tohumlanmış satırı siler
2. POST /billing/bills/{id}/comp: izni olan doğrudan uygular, olmayan yetki motoruna sorar (Uygulandı/Beklemede/Reddedildi), ikram gerekçesi ikram kataloğundan, indirim gerekçesi bu uç noktada geçersiz, /discount ikram gerekçesini kabul etmez
3. Kasa ekranı: İkram bölümü (tutar, kalanın tamamı, gerekçe, not), beklemede anahtar korunur, onay durumunu kontrol et, reddedildi mesajı Türkçe
4. Gerçek Postgres testleri ve mutasyon

## Out of scope

- Kalem düzeyi kasa ikramı, indirim beklemedeki anahtar düzeltmesi (rapor edilir), raporlarda ikram ayrıştırması, React değişikliği (sınır ayarı ekranı zaten var)

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İleri/geri migration kanıtı
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
