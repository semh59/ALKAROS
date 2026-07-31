# Task Scope Enforcement Contract (V1-FND-003)

Aktif tek `Task ID` için izin verilen write-set dışındaki her dosya değişikliğini local ve CI ortamında
fail-closed olarak reddeden sözleşme. Uygulama: `tools/task-scope/task_scope_tool.py`.

## Input

- `--task-id` (zorunlu): `V<version>-<KISALTMA>-<sayi>` biçiminde tek Task ID. `plan/` altında ilgili görev Markdown
  dosyası aranır.
- `--repo-root` (opsiyonel): Git repository kökü. Varsayılan: araç dosyasının iki üst dizini.
- `--plan-dir` (opsiyonel): Görev Markdown dosyalarının dizini. Varsayılan: `plan/`.
- `--format` (opsiyonel): `json` (varsayılan) veya `text`.

## Output

- `json`: stdout'a makine tarafından okunabilir tek JSON nesnesi yazılır:
  - `task_id`: doğrulanan Task ID.
  - `valid`: tüm metadata ve path kontrollerinin başarısı.
  - `metadata_errors`: string listesi — görev metadata doğrulama hataları.
  - `findings`: nesne listesi — her bulgu `path`, `change_type`, `reason` anahtarlarını içerir.
- `text`: insan okunur `OK:` / `FAIL:` özeti, bulgu ve metadata hataları satır satır listelenir.
- Çıktı listeleri sıralıdır; aynı input aynı sıralı çıktıyı üretir.

## Exit codes

- `0`: `valid == true` — her değişen yol allowlist'te, metadata geçerli.
- `1`: herhangi bir metadata hatası veya allowlist dışı path bulgusu (fail-closed).

## Allowlist

Görev Markdown'ının `Owned surface` bölümündeki her yol, görev dosyasının kendi yolu ve `evidence/<Task-ID>/**`
allowlist'i oluşturur. Kontrol edilen değişiklikler: staged, unstaged, untracked, deleted ve renamed yollar.
Rename'de eski ve yeni yolun ikisi de allowlist'te olmalıdır.

## Path normalizasyonu ve glob

- Backslash `/`'ye çevrilir, `./` başlangıcı atılır ve tüm yol lowercase yapılır (Windows case-insensitive).
- `**` (her şey dahil `/`), `*` (tek segment), `?` (tek karakter) desteklenir; diğer karakterler literal eşleşir.
- `..` dizin traversal segmenti içeren yol her koşulda reddedilir.

## Fail-closed durumlar

- Görev dosyası `plan/` altında bulunamaz.
- Görev dosyası birden fazla veya hiç Task ID içermez; Task ID biçimi geçersizdir.
- `Status` değeri `Planned`, `InProgress`, `Done`, `Blocked` dışındadır.
- `Assignee` boştur veya genel (`codex`, `ai`, `none`, `unassigned*`) tanımlıdır.
- Bağımlılıklardan herhangi biri `Done` değildir.
- Değişen bir yol allowlist'te değildir veya traversal içerir.

## Failure recovery

- Araç hiçbir dosyayı değiştirmez, geri almaz veya otomatik düzeltmez; yalnız kesin path ve gerekçe raporlar.
- Local kullanım: `--format text` ile bulgu listesi incelenir, hatalı değişiklik uygun görev kapsamına taşınır veya
  geri alınır, komut yeniden çalıştırılır.
- CI: non-zero exit required check'i kırar; aynı fixture seti local komut ve CI'da aynı exit code ve sıralı finding
  listesini üretir.
- Dependency `Done` değilse önce dependency görevi kanıtla kapatılır, sonra aktif görev doğrulanır.
