# V1-RMD-838 - Hesap ve kasa defteri bütünlük düzeltmesi: kalem silme koruması, doğrulanmamış kısıtlar, defter değiştirilemezliği

- Task ID: V1-RMD-838
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi G11 grubu, kayıtlar F0351 (V1-BIL-004), F0433 kalem silme kısmı (V1-RMD-732), F0434 (V1-RMD-732), F0440 (V13-CSH-002). Uygulanmış migration dosyaları sağlama toplamıyla korunduğu için düzeltme yeni migration 218 ile yapılır:

1. `fk_bill_adjustments_item` ve `fk_bill_allocations_item` (kalem -> denetimli indirim ve bölüşüm satırı) `ON DELETE CASCADE` yerine `NO ACTION`: tek bir hesap kalemi silinince ona bağlı denetimli ayar/bölüşüm satırı sessizce kaybolmaz; hesabın tamamı silinince (hesap -> kalem -> satır zinciri) silme sürer. Üretim kodunda hesap kalemi silme yolu yoktur (iptal `superseded_at` ile işaretler).
2. `ck_bills_nonnegative_amounts` ve `fk_bills_cancelled_by` (209 `NOT VALID` bıraktı) doğrulanır (`VALIDATE CONSTRAINT`); eski satır ihlal ediyorsa migration açık veritabanı hatasıyla durur.
3. `cash.cash_transactions` için satır bazlı `UPDATE`/`DELETE` engelleyici tetikleyici (ekleme-yalnız defter). Kullanıcı silinince yabancı anahtarın `recorded_by` alanını boşaltması (`ON DELETE SET NULL`) tek izin verilen güncellemedir.

## Owned surface

- `evidence/V1-RMD-838/**`
- `database/migrations/V1/V1-RMD-838/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs (V1-RMD-698) — bütünlük ve geri alma testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test (gerçek Postgres): tek kalem silme ayar satırı varken reddedilir, hesabın tamamı silinir; iki `NOT VALID` kısıt doğrulanmış (`convalidated`); kasa defterinde `UPDATE` ve `DELETE` reddedilir, kullanıcı silme (`recorded_by` boşaltma) geçer; ihlalli satır varken doğrulama açık hatayla durur; geri alma ve tekrar uygulama.
2. Düzeltme, yeşil, mutasyon turu (`cp` + `cmp` ile geri alınır).
3. Migration listesi/kayıt dosyaları (`order.json`, `PhaseBMax`, manifest testi).
4. İngilizce kod ve test adları.

## Out of scope

- 211 numaralı migration'ın `NOT VALID` kısıtı (eski satırlar gerekçesiz olabilir); `CustomFee` kaldırma (V1-RMD-837); ödeme iade defteri (zaten ekleme-yalnız).

## Dependencies

- V1-RMD-837

## Acceptance evidence

1. Migration testleri, Manifest testleri, Billing ve Cash modül testleri ile Host Billing/CashSession testleri yeşil; kırmızı ve yeşil çıktı `evidence/V1-RMD-838/` altında.
2. Mutasyon: her madde için kuralı gevşeten mutant testleri kırar.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
