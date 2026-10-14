# V1-RMD-524 - Ürün ve adet bazlı kısmi iade isteği (sunucu)

- Task ID: V1-RMD-524
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

PO kararları: iade adet seçilerek yapılır; kasiyer iadenin hangi ödemeden düşeceğini seçer; kasiyer stoğa geri dönüşü seçer; hesap Ödendi kalır; tutar bütçe ve onay kurallarına (V1-RMD-520) uyar. İstek, hesap kalemi ve adet satırları, ödeme ve stok seçimiyle açılır; tutar sunucuda kalemin birim fiyatı × adet (ayarlama payı dahil) olarak hesaplanır; tamamlanınca kısmi ters kayıt ve (seçildiyse) stok geri dönüşü aynı işlemde yazılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-524-item-refund-request-backend.md`
- `evidence/V1-RMD-524/**`
- `database/migrations/V1/V1-RMD-524/186-refund-request-items.up.sql`
- `database/migrations/V1/V1-RMD-524/186-refund-request-items.down.sql`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/PaymentRefunds.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitApplication.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/PaymentAggregate/PostgresRefundRequestRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/PaymentRefundHttpTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yeni tablo: iade isteği kalemleri (bill_item_id, adet, stoğa dön); kalem başına iade edilen adet toplamı kalemin adedini aşamaz; aynı ödeme için tek açık istek kuralı sürer.
- Tutar sunucuda hesaplanır: kalemin brüt tutarı × adet / kalem adedi × hesap seviyesi indirim oranı ((orijinal toplam - indirimler) / orijinal toplam), 2 ondalığa yuvarlanır; bahşiş ve hizmet bedeli iade edilmez (varsayım, PO onayı beklenir).
- Stoğa dönüş seçilen kalemler için V1-RMD-523 hizmetiyle iade tamamlandığı işlemde yazılır; hizmet kayıtlı değilse (test ortamı) stok seçimi reddedilir.
- İsteğe iade edilen kalemler (kalem kimliği, adet, stoğa dön) yanıtta ve listede döner.
- İade tutarı ödemenin kalan iade edilebilir tutarını aşamaz; tam tutar ödemenin tamamıysa mevcut tam iade yolu.
- Gerçek Host + Postgres testleri; migration manifest birlikte güncellenir.

## Out of scope

- Arayüz (V1-RMD-525), fiş ve rapor (V1-RMD-526).

## Dependencies

- V1-RMD-520
- V1-RMD-522
- V1-RMD-523

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-524/` altındadır.

## Handoff

- None
