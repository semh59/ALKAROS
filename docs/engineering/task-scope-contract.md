# Task Scope Enforcement Contract (V1-FND-003)

Aktif tek `Task ID` için izin verilen write-set dışındaki her dosya değişikliğini local ve CI ortamında
fail-closed olarak reddeden sözleşme. Uygulama: `tools/task-scope/task_scope_tool.py`.

## Input

- `--task-id` (zorunlu): `V<version>-<KISALTMA>-<sayi>` biçiminde tek Task ID. `plan/` altında ilgili görev Markdown
  dosyası aranır.
- `--repo-root` (opsiyonel): Git repository kökü. Varsayılan: araç dosyasının iki üst dizini.
- `--plan-dir` (opsiyonel): Görev Markdown dosyalarının dizini. Varsayılan: `plan/`.
- `--format` (opsiyonel): `json` (varsayılan) veya `text`.
- `--diff-base` (opsiyonel): Base ref. Verildiğinde değişen yollar worktree yerine
  `git diff --name-status <base>... HEAD` çıktısından toplanır (CI PR/dispatch modu); verilmediğinde worktree
  `git status --porcelain=v1` modu (local preflight) kullanılır.
- `--candidate-remediation` (opsiyonel): yalnız kayıtlı candidate-code remediation görevinin mevcut kusurunu,
  kapalı dependency zincirini kabul kanıtı saymadan düzeltmek için kullanılır.

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

Görev Markdown'ının `Owned surface` bölümündeki her yol ve `evidence/<Task-ID>/**`
allowlist'i oluşturur. Görev Markdown dosyasının kendisinde yalnız metadata `Status` ve `Assignee` satırları
değişebilir. `Blocked` ile `Planned` veya `InProgress` arasındaki geçişte zorunlu `Blocker`
bölümü de eklenebilir veya silinebilir; başlık, `Owned surface` veya başka bir bölüm
allowlist değildir. Yalnız path şekilli backtick parçaları
(içinde `/`, `\`, `.`, `*` veya `?` bulunan) allowlist
ögesi sayılır; serbest metin, task ID ve diğer backtickli kelimeler yok sayılır. Kontrol edilen değişiklikler:
worktree modunda staged, unstaged, untracked, deleted ve renamed yollar; diff modunda base ile HEAD arasındaki
committed değişiklikler. Rename'de eski ve yeni yolun ikisi de allowlist'te olmalıdır.

## Path normalizasyonu ve glob

- Backslash `/`'ye çevrilir, `./` başlangıcı atılır ve tüm yol lowercase yapılır (Windows case-insensitive).
- `**` (her şey dahil `/`), `*` (tek segment), `?` (tek karakter) desteklenir; diğer karakterler literal eşleşir.
- Wildcard yüzeyler, exact path'lerden sonra değerlendirilir; aynı dosyayı eşleyen birden fazla yüzey kapsam dışı
  sayılır.
- `..` dizin traversal segmenti içeren yol her koşulda reddedilir.

## Fail-closed durumlar

- Görev dosyası `plan/` altında bulunamaz veya Git'te committed baseline'ı yoktur.
- Görev dosyası birden fazla veya hiç Task ID içermez; Task ID biçimi geçersizdir.
- `Status` değeri `Planned`, `InProgress`, `Done`, `Blocked`, `NotApplicable` dışındadır.
- `Assignee` boştur veya genel (`codex`, `ai`, `none`, `unassigned*`) tanımlıdır.
- `--candidate-remediation` kullanılmadığında bağımlılıklardan herhangi biri `Done` değildir.
- Değişen bir yol allowlist'te değildir veya traversal içerir.
- Görev Markdown değişikliği `Status` veya `Assignee` metadata satırı dışında bir satıra dokunur.
  Tek istisna, `Blocked` ile `Planned` veya `InProgress` arasındaki geçişte eksiksiz
  `Blocker` bölümünün eklenmesi veya silinmesidir.
- `Status` değeri `Planned` veya gerçek oturum sahibine atanmış `InProgress` değildir.
- Sürüm entry gate'i, önceki sürümdeki her görevin `Done` veya kanıtlı
  `NotApplicable` kaydıyla kapandığı ispatlanamamıştır.
- `GATES.md` içindeki remediation exception tablosunun marker'ı,
  başlığı, ayıracı, satır biçimi veya exact Task ID kümesi geçersizdir;
  yinelenen veya onaysız Task ID kaydı vardır.

## Kanıtlanmış bulgu remediation istisnası

`GATE-V0-EXIT` açıkken `check_entry_gate`, yalnız `GATES.md` içindeki
`TASK_SCOPE_REMEDIATION_EXCEPTIONS` marker'ları arasındaki katı tablodan
ayrıştırılan exact ID'leri kabul eder. 2026-08-02 ve 2026-08-03 kullanıcı onaylarının sabit
kümesi GATES.md ile araç kodunda birebir eşleşir.

Tablo eksik, bozuk, yinelenen veya bu kümeyle eşleşmeyen bir kayıt içerirse
denetim fail-closed olarak non-zero exit verir. İstisna yalnız kanıtlanmış
bulguyu düzeltmeye yarar; V0/V1 gate kapanış kanıtı değildir ve yeni product
behavior üretme izni vermez.

`--candidate-remediation`, yalnız sabit candidate-code kimlikleri için kullanılabilir. Bu modda görev
`InProgress` ve gerçek bir oturum sahibine atanmış olmalıdır; yazılabilir yüzey değişmez. Mod, görevi
`Done` yapmaz, dependency veya gate'i kapatmaz ve yeni ürün davranışı eklemeye izin vermez.

## Failure recovery

- Araç hiçbir dosyayı değiştirmez, geri almaz veya otomatik düzeltmez; yalnız kesin path ve gerekçe raporlar.
- Local kullanım: `--format text` ile bulgu listesi incelenir, hatalı değişiklik uygun görev kapsamına taşınır veya
  geri alınır, komut yeniden çalıştırılır.
- CI: `--diff-base` ile PR base SHA'sına göre çalışır; temiz worktree diff modunda sonucu bozmaz. Aynı fixture seti
  local komut ve CI'da aynı exit code ve sıralı finding listesini üretir (worktree modu local, diff modu CI için;
  her iki modun çıktı sözleşmesi aynıdır).
- Normal görevde dependency `Done` değilse önce dependency görevi kanıtla kapatılır, sonra aktif görev doğrulanır.
  Kayıtlı candidate-code remediation görevi bu kuralın yalnız düzeltme çalışması istisnasıdır.

## Task Markdown immutability

- Araç, worktree modunda görev Markdown'ını `HEAD` sürümüyle; diff modunda ise
  `merge-base` sürümüyle karşılaştırır.
- Yalnız `Status` ve `Assignee` metadata satırları değişebilir. `Blocked` ile
  `Planned` veya `InProgress` arasındaki geçişte yalnız `Blocker` bölümünün tamamı
  da değişebilir. `Owned surface`, Goal, dependency veya başka herhangi bir satırdaki
  fark fail-closed bulgudur.
- Baseline'da olmayan yeni görev Markdown'ı write-set doğrulaması için güvenilir
  input değildir ve reddedilir. Plan görevi önce ayrı plan denetimiyle
  kaydedilmelidir; kendi untracked dosyasından write scope üretemez.
