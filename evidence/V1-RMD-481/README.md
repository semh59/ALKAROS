# V1-RMD-481 - Denetim kayıtlarının 10 yıl sonra bölüm bırakılarak silinmesi

`audit.audit_events` artık `occurred_at` yılına göre bölümlüdür (2020-2060 arası yıllık bölümler + hiçbir zaman bırakılmayan varsayılan bölüm).
Ekleme-yalnız tetikleyicisi ana tabloda ve her bölümde çalışır; satır güncellenemez/silinemez. Yeni `audit-disposal` komutu, yılı 10 yıl dolan bölümü
bırakır: yıl Y, Y+11'in 1 Ocak'ında (UTC) süresi dolar. Varsayılan kuru çalıştırmadır.

## Değişiklikler

- Migration `173` (`database/migrations/V1/V1-RMD-481/`): eski tabloyu bölümlüye taşır (satır sayısı doğrulanır, yinelenebilir), indeksleri ve tetikleyiciyi yeniden kurar; geri alma satırları düz tabloya geri kopyalar.
  Birincil anahtar `(id, occurred_at)` oldu: kimliğin tablo genelinde tekilliği artık veritabanınca garanti edilmez (kimlikler rastgele UUID, yabancı anahtar yok).
- `src/Modules/Audit/PartitionDisposal/AuditPartitionDisposal.cs`: bölümleri listeler, süresi dolanı bırakır, bırakılan bölümün adı/yılı/satır sayısını (içeriksiz) `audit.partition.disposed` olayı olarak yazar.
  Adı ve sınırları tam bir takvim yılı olmayan bölüme dokunmaz.
- `src/Host/Program.cs`: `audit-disposal --db-url <url> [--apply] [--as-of <tarih>]`; `--as-of` yalnız önizleme, `--apply` ile birlikte reddedilir.
- Bağlantılar: `order.json`, `MigrationManifest` (PhaseBMax 173), `ManifestTests`, `docs/compliance/kvkk-retention-runbook.md`.

## Kanıt

- `migration-up-down.log`: gerçek Postgres'te 015 sonrası 3 satır (2014/2026/şimdi) → 173 ile yıl bölümlerine dağıldı; ikinci uygulama etkisiz; ana tabloda ve bölümde doğrudan UPDATE/DELETE reddedildi; 1999 ve 2100 tarihli satırlar varsayılan bölüme girdi; geri alma 5 satırı korudu; yeniden uygulama.
- `tests-host-focused.log`, `tests-host.log`: `AuditDisposalTests` (bölümleme + ekleme-yalnız, sınır günleri, kuru çalıştırma/uygulama/ikinci uygulama, komut) ve `ManifestTests`; tam Host test projesi.
- `mutation.log`: 4 mutant (süre bir yıl erken doluyor, kuru çalıştırma da bırakıyor, bırakma kaydı yok, tetikleyici DELETE'i bırakıyor) kırmızı; dosyalar geri alındı, `fc /b` özdeş.
- `gercek-deneme.log`: Host komutu gerçek Postgres'te: 2014, 2015, 2026 ve bugünün satırları; kuru çalıştırma hiçbir şeyi değiştirmez; `--apply --as-of` reddedilir; `--apply` yalnız 2014 ve 2015'i bırakır; ikinci çalıştırma etkisiz; olaylar satır sayısı ile kayıtlı.

## Açık kalan

- 10 yıl kuralının muhasebeci/avukat tarafından onayı bekliyor; mekanizma onaydan bağımsız. Hiçbir kayıt 2031'den (2020 yılı) önce silinemez; bugünkü 2026 kayıtları 2037'ye kadar durur.
- Komutun düzenli çalıştırılması `V1-RMD-482`.
- 2060 sonrası için bölüm açılması gerekir (varsayılan bölüm o zamana kadar tolere eder).
- Denetim için yasal tutma sınıfı yoktur.
