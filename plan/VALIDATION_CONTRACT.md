# Plan Doğrulama Sözleşmesi

Bu belge plan değişikliklerinin tekrar edilebilir kabul koşullarını tanımlar.
Komut çıktısı olmadan hiçbir kontrol geçmiş sayılmaz.

## Baseline kontrolleri

- Kaynak PDF SHA-256 değeri `PDF_SOURCE.md` ile aynı olmalıdır.
- PDF 94 sayfa ve encrypted değeri `false` olmalıdır.
- Başlangıç manifesti 211 Markdown dosyası ve 8.658 satır içermelidir.
- Başlangıçtaki her dosya `AUDIT_REPORT.md` içinde ilk hash ile yer almalıdır.

## Tekrarlanabilir denetim ortamı

- Python runtime `3.12.12`, `uv 0.10.7`; runtime pin'leri
  `plan/validation-runtime.lock`, PDF dependency set'i
  `plan/validation-requirements.lock` içindeki exact sürümlerdir.
- Markdown runtime `markdownlint-cli2@0.23.2`; exact komut
  `npx --yes markdownlint-cli2@0.23.2` ve sürüm kaydı
  `plan/validation-node-requirements.lock` içindedir.
- PDF dışı `validate` ve `verify-manifest` komutları PDF package import etmeden
  çalışır. `validate-coverage` kilitli dependency ortamında çalıştırılır.
- Markdown lint kök `.markdownlint-cli2.jsonc` dosyasını kullanır; örtük local
  config veya process timeout'u başarılı sonuç sayılmaz.
- Doğrulama aracı kalıcı `tools/plan-audit/plan_audit_tool.py` yolundadır;
  `tmp/` altındaki script veya çıktı kabul kanıtı değildir.
- Tekrarlanabilir komutlar:

```text
uv run --python 3.12.12 --with-requirements plan/validation-requirements.lock python tools/plan-audit/plan_audit_tool.py validate
uv run --python 3.12.12 --with-requirements plan/validation-requirements.lock python tools/plan-audit/plan_audit_tool.py validate-coverage
uv run --python 3.12.12 --with-requirements plan/validation-requirements.lock python tools/plan-audit/plan_audit_tool.py verify-manifest
npx --yes markdownlint-cli2@0.23.2
```

## Görev şeması kontrolleri

- Her görev başlığı ve dosya adı aynı tekil `Task ID` değerini taşır.
- `TASK_STANDARD.md` içindeki metadata ve bölüm sırası eksiksizdir.
- `Blocked` görevde bir `Blocker` bölümü vardır; diğer durumlarda yoktur.
- `Dependencies` ve `Handoff` yalnız mevcut task ID, gate ID veya `None` içerir.
- Dependency graph döngüsüzdür.
- `InProgress`, `NotApplicable` ve `Done` görevlerde tek gerçek assignee bulunur.
- `NotApplicable` yalnız tamamlanmış, tarihli ve gerçek assignee taşıyan decision
  kanıtıyla kapanır; task dosyası ve dependency kimliği korunur.
- `NotApplicable` dependency yalnız consumer acceptance bu sonucu adıyla ele
  alıyorsa terminaldir; aksi durumda consumer başlatılamaz.
- Bütün görevlerde en az bir geçerli `Source basis` bulunur.
- Mevcut kod ağacı oluşmadığı sürece bütün görevlerde `Surface state: Planned` olur.
- Repository kökündeki `AGENTS.md`, tek `Task ID` ve fail-closed Codex write-set
  sözleşmesini içerir.
- `V1-FND-001`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`,
  `V1-SEC-002`, `V1-FND-002` ve `V1-FND-006` direct dependency zinciriyle bu
  sırada tamamlanır; zincir bitmeden başka application görevi `InProgress` olamaz.
- 2026-08-01 kullanıcı onayıyla `V1-FND-007` (audit remediation) zincirden önce
  başlatılabilir; karar kaydı `TRACEABILITY.md` FIND-IA-0027'dir ve diğer
  application görevleri için zincir kuralı değişmez.
- 2026-08-01 kullanıcı onayıyla `V1-FND-008` (boundary audit round 2) da aynı
  istisna kapsamında zincirden önce başlatılabilir; karar kaydı
  `TRACEABILITY.md` FIND-IA-0037'dir ve zincir kuralı diğer application
  görevleri için değişmez.
- Codex write allowlist yalnız `Owned surface`, aktif görev metadata alanları ve
  `evidence/<Task-ID>/**` birleşimidir.
- Scope doğrulaması staged, unstaged, untracked, deleted ve rename işleminin her
  iki yolunu kapsar; allowlist dışı tek yol non-zero exit üretir.

## İçerik kontrolleri

- Belirsiz “PDF baseline plus gap”, “all tasks”, “owners” ve genel
  “production implementation” kalıpları sıfır olmalıdır.
- Türkçe açıklama içinde code, API, provider ve status terimleri özgün English
  biçiminde kalabilir; bütünüyle English açıklama cümlesi kalamaz.
- Her implementation görevi başarı, ret/failure ve ilgiliyse retry/concurrency
  kanıtını ister.
- Migration isteyen görev ileri/geri uygulama kanıtını aynı görevde ister.
- PDF dışı security, accessibility ve release policy maddeleri yalnız `EXT` veya
  tamamlanmış `DEC` kaynağına dayanır.

## PDF coverage kontrolleri

- PDF'deki 374 adet `I.*`, `II.*`, `III.*`, `IV.*` başlığı ve `C1-C9`
  kayıtları coverage matrisinde tam olarak bir kez yer alır.
- PDF'nin 94 sayfasından çıkarılan 2.725 non-empty text line; page, parent
  section, class, tam normalize text SHA-256, owner ve disposition taşır.
- List item ve normative expression sınıfları line matrix içinde ayrıca
  etiketlenir; sınıflandırılmayan satır da `Content` olarak kaybedilmeden kalır.
- `pdfplumber` geometry detector tarafından bulunan 178 table-like row ayrıca
  page/bounding-order temelli unit kimliği ve tam normalize cell-text SHA-256
  değeriyle izlenir. Bu sınıf semantik tablo varsayımı değildir.
- Regeneration aynı 374/9/2.725/178 sayılarını, parent/owner değerlerini ve aynı
  unit hash'lerini üretir.
- `II.16` plan gereksinimi olarak oluşturulmaz; belge haritası finding'i olarak
  tutulur.
- `I.46` 14/13 çelişkisi `CORR:C8` ve `V0-DOC-001` ile izlenir.

## Markdown kontrolleri

- `markdownlint-cli2` sonucu sıfır hata olmalıdır.
- Prose line length 120 karakterdir.
- Table row, code block ve bölünemeyen URL satırları `MD013` istisnasıdır.
- `MD012` ve `MD060` istisnasız sıfır olmalıdır.

## Kapanış

İlk doğrulamadan sonra aynı kontroller taze bağlamlı bağımsız denetimde yeniden
çalıştırılır. İkinci çalıştırma sıfır hata üretmeden Git hazırlık işi başlamaz.
`AUDIT_MANIFEST.json`; bütün Markdown dosyalarının yanında `AGENTS.md`, lint
config, runtime/dependency lock'ları, baseline manifest ve kalıcı audit tool
hash'lerini de doğrular.

Mevcut koşullu Markdown sayısı artık sabit değil: `verify-manifest` sayıyı
diskteki `plan/**/*.md` dosyalarından türetir; meal-card provider kararı
eklendikçe `V12-MCD-1xx` dosyaları diskte göründüğünde sayı otomatik artar.
Plan dosyası eklemek/çıkarmak için plan-audit aracında hard-code düzenlemesi
gerekmez. Licensing task dosyaları `NotApplicable` sonucunda da korunur;
bilinmeyen karar için sayı uydurulmaz.
