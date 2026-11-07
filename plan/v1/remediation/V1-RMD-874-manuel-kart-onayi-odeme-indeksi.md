# V1-RMD-874 - Manuel kart onayı tablosuna ödeme ve durum üzerinde indeks (migration 221)

- Task ID: V1-RMD-874
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

payments.manual_card_confirmations için yalnız Pending'i kapsayan kısmi indeks var (payment_id WHERE status = 'Pending'). Ödeme yöntemi çözümlemesi (PaymentMethodLookup.Sql: m.payment_id = X AND m.status = 'Approved'; iade isteği ve rapor sorguları) Approved durumunu arar; indeks kullanılamaz ve tablo taranır, FK da otomatik indeks üretmez. Gerçekten çalışan bu sorgu için (payment_id, status) üzerinde adlı bir indeks eklenir (migration 221, geri alınabilir).

## Owned surface

- `evidence/V1-RMD-874/**`
- `database/migrations/V1/V1-RMD-874/221-manual-card-confirmations-payment-index.up.sql` — indeks.
- `database/migrations/V1/V1-RMD-874/221-manual-card-confirmations-payment-index.down.sql` — geri alma.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax 221.
  - database/MigrationComposition/order.json — 221 kaydı ve aralık.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — manifest sayımı.
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs — indeks, geri alma ve yeniden uygulama testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: 221 uygulanınca ix_manual_card_confirmations_payment_status vardır ve (payment_id, status) kolonlarını kapsar; geri alınca kalkar; yeniden uygulanır.
2. İleri ve geri kanıtı evidence/V1-RMD-874/ altında.
3. Mutasyon: indeks kolonlarının ve geri alma betiğinin bozulması testi düşürür (cp+cmp geri alınır).

## Out of scope

- Mevcut Pending kısmi indeksini silmek veya başka tabloya indeks eklemek.

## Dependencies

- None

## Acceptance evidence

1. Host MigrationComposition testleri gerçek Postgres ile exit 0; ileri ve geri kanıtı evidence/V1-RMD-874/.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
