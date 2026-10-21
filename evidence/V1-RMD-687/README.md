# V1-RMD-687 — Ayar geçmişi değişmez, logo tablosu sertleşir (göç 197)

## Yapılan
- `settings.setting_history`: satır silinemez; denetlenen hiçbir kolon değiştirilemez (tek izin: kullanıcı silinince mevcut ON DELETE SET NULL'un `changed_by`'ı boşaltması). Ayar geçmişi varken silinemez (FK ON DELETE CASCADE -> RESTRICT).
- `settings.business_logo`: içerik türü yalnız png/jpeg/webp, boyut 1 bayt – 5 MB (NOT VALID: mevcut satırlar taranmaz, yeni/değişen satırlar denetlenir). `row_version` kolonu belgelendi: her değişimde artar, eşzamanlılık için kullanılmıyor (ETag `updated_at`'ten gelir; "ölü kolon" bulgusu kısmen yanlıştı).
- Down betiği 026'daki hâle döner; testte down -> düzenlenebilir/cascade -> up yeniden çalışır.
- Kayıtlar: order.json, MigrationManifest PhaseBMax=197, ManifestTests, rehearsal profili.

## Kanıt
- Host.Tests (göç zinciri) 196/196, Host Settings 56/56, TypedSettings 66/66, BusinessIdentity 32/32.
- mutations.log: 15/15 mutant KILLED.

## Doğrulanamadı
- Büyük/gerçek veride göç süresi ölçülmedi (history tablosu küçük; rehearsal profiline not düşüldü).
- Tarayıcıda elle deneme yok (arayüz değişmedi).
