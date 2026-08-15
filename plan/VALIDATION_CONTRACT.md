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
- `Done` görevin bütün doğrudan task dependency'leri `Done` olmalıdır.
- `Done` görevin bütün transitive task dependency zinciri `Done` olmalıdır.
- Bu iki status kontrolü sırasıyla `DONE_DEPENDENCY_NOT_FINAL` ve
  `DONE_DEPENDENCY_TRANSITIVE_NOT_FINAL` hatalarıyla fail-closed çalışır.
- `InProgress`, `NotApplicable` ve `Done` görevlerde tek gerçek assignee bulunur.
- `NotApplicable` yalnız tamamlanmış, tarihli ve gerçek assignee taşıyan decision
  kanıtıyla kapanır; task dosyası ve dependency kimliği korunur.
- `NotApplicable` dependency yalnız consumer acceptance bu sonucu adıyla ele
  alıyorsa terminaldir; aksi durumda consumer başlatılamaz.
- Bütün görevlerde en az bir geçerli `Source basis` bulunur.
- Mevcut kod ağacı oluşmadığı sürece bütün görevlerde `Surface state: Planned` olur.
- Repository kökündeki `AGENTS.md`, tek `Task ID` ve fail-closed Agent write-set
  sözleşmesini içerir.
- Mevcut Git geçmişi ve application ağacı candidate evidence'dır; V0 altında açık
  `Blocked` görev varken `implementation` veya `integration` türündeki V1+ görevi
  `InProgress` ise `APPLICATION_STARTED_BEFORE_V0_EXIT` hatası üretilir.
- `V1-FND-001`, `V1-FND-010`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`,
  `V1-SEC-002`, `V1-FND-002` ve `V1-FND-006` direct dependency zinciriyle bu
  sırada tamamlanır; zincir bitmeden başka application görevi `InProgress` olamaz.
- 2026-08-01 kullanıcı onayıyla `V1-FND-007` (audit remediation) zincirden önce
  başlatılabilir; karar kaydı `TRACEABILITY.md` FIND-IA-0027'dir ve diğer
  application görevleri için zincir kuralı değişmez.
- 2026-08-01 kullanıcı onayıyla `V1-FND-008` (boundary audit round 2) da aynı
  istisna kapsamında zincirden önce başlatılabilir; karar kaydı
  `TRACEABILITY.md` FIND-IA-0037'dir ve zincir kuralı diğer application
  görevleri için değişmez.
- 2026-08-01 kullanıcı onayıyla ("DÜZELT") `V1-FND-009` (pushed history
  rewrite + force-push) da aynı istisna kapsamında zincirden önce
  başlatılabilir; karar kaydı `TRACEABILITY.md` FIND-IA-0050'dir ve zincir
  kuralı diğer application görevleri için değişmez.
- `GATES.md` içindeki `TASK_SCOPE_REMEDIATION_EXCEPTIONS` tablosu, 2026-08-10
  tarihli `CORR:C52` kaynaklı 18 kayıt ile yalnız `V1-FND-023` için 2026-08-11
  tarihli `CORR:C52;CORR:C53;CORR:C54` kaydından oluşan exact 19-ID admission
  tuple'ını makinece doğrular. Mevcut `Done` görevler kabul kümesine giremez veya
  yeniden açılamaz. Kayıtlı candidate-code remediation kimliği
  `--candidate-remediation` ile yalnız mevcut kanıtlanmış kusuru düzeltebilir;
  açık dependency veya gate kabul kanıtı sayılmaz. Historical PDF current
  remediation authority değildir.
- `validate`, aşağıdaki contract tablosunu, `GATES.md` tablosunu ve task-scope
  canonical record'larını exact source/date/ID değerleriyle doğrular. Count,
  duplicate, order, extra, missing, source veya date divergence deterministic
  `SEMANTIC_REMEDIATION_ADMISSION_*` hatasıyla non-zero exit verir.
- C54, açık V0 gate altında yalnız `V1-FND-023` `InProgress` olduğunda istisnadır.
  Validator statik olarak `TRACEABILITY.md` C54 exact authority satırını,
  `V1-FND-023`ün exact `Directory.Build.targets` owned surface'ini, C52/C53/C54
  source zincirini, üç canonical 19-ID tuple'ını ve `V0-GOV-050` ile
  `V1-FND-001` Done dependency'lerini birlikte doğrular. Missing, expanded veya
  malformed authority; wrong source/date/tuple; open dependency; `Done` status
  veya başka V1 application deterministic semantic hata üretir; target/tool code
  yürütülmez.

<!-- PLAN_AUDIT_REMEDIATION_ADMISSION:START -->
| Task ID | Approval date | Source basis | Purpose | Gate closure evidence | New feature behavior |
| --- | --- | --- | --- | --- | --- |
| `V1-FND-016` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-017` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-018` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-019` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-020` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-021` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-022` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-FND-023` | `2026-08-11` | `CORR:C52;CORR:C53;CORR:C54` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-006` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-007` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-008` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-009` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-010` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-011` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-012` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-IAM-013` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-SEC-004` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-SEC-005` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
| `V1-CAT-003` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |
<!-- PLAN_AUDIT_REMEDIATION_ADMISSION:END -->
- `GATES.md` içindeki `V0_DEFERRED_TASKS` marker tablosu 2026-08-03 kullanıcı
  onaylı devir listesini makinece doğrular (`TRACEABILITY.md` C40); 2026-08-13
  kullanıcı onaylı eklemeler (`TRACEABILITY.md` C65, C66) ile
  `V0-REV-001..030`, `V0-GOV-041` ve `V0-GOV-042` da listededir. Listede
  olmayan V0 görevi `Blocked` ise `APPLICATION_STARTED_BEFORE_V0_EXIT` hatası
  üretilmeye devam eder; listedeki görevler `Blocked` kalır, kanıtlarını ilgili
  aşamada (V12-V20) toplar ve `GATE-V0-EXIT` kapanış kanıtı sayılmaz.
  `DEPENDENCY_REMOVALS`/forbidden seti dışında dependency düzenlemesi kabul
  edilmez; devir yeni product behavior başlatma izni vermez.
- 2026-08-04 kullanıcı onayıyla (`TRACEABILITY.md` C44) task-scope aracının
  `GATE-V0-EXIT` türetilmiş entry-gate kontrolü, `V0_DEFERRED_TASKS` tablosunu
  fail-closed okur ve 43 devir kimliğini yalnız bu gate'in kapanma koşulundan
  muaf sayar; kayıt kümesi GATES.md ile araç kodunda birebir eşleşir, tablo
  bozuk/yinelenen/eksikse denetim non-zero exit verir, `GATES.md` yoksa gate
  açık listesiyle reddedilir. Muafiyet yalnız `GATE-V0-EXIT` türetimi içindir,
  remediation exception mekanizmasını değiştirmez, V0 kapanış kanıtı üretmez
  ve yeni product behavior başlatma izni vermez.
- Tablo marker'ı, başlığı, ayıracı, satır biçimi, approval tarihi veya exact
  Task ID kümesi bozuksa; yinelenen ya da ek bir kayıt varsa task-scope
  denetimi fail-closed non-zero exit verir. İstisna V0/V1 gate kapanış kanıtı
  değildir ve yeni product behavior başlatma izni vermez.
- Agent write allowlist yalnız `Owned surface`, aktif görev metadata alanları ve
  `evidence/<Task-ID>/**` birleşimidir.
- Aktif görev `Blocked` ile `Planned` veya `InProgress` arasında geçerken yalnız
  kendi zorunlu `Blocker` bölümünü ekleyebilir veya silebilir; başka görev gövdesi
  değişikliği fail-closed reddedilir.
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
  kayıtları coverage matrisinde tam olarak bir kez yer alır. (2026-08-03:
  FIND-IA-0004 doğrulamasına göre 375→374 düzeltildi; C38.)
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

## Kapanış kanıt zarfı

- `Done` için command, integer exit code `0`, environment, candidate Git commit,
  raw command output ve SHA-256 artifact hash'leri machine-readable closure
  evidence envelope içinde birlikte doğrulanır.
- Candidate commit artifact blob'unu içermeli; candidate ile güncel `HEAD`
  arasında artifact değişmişse veya final blob hash'i farklıysa kanıt
  fail-closed reddedilir.
- Raw output, `evidence/<Task-ID>/` altında kalır ve secret value içeremez.
  Sensitive environment girdileri yalnız redacted `env:<NAME>` location ve
  SHA-256 fingerprint ile kaydedilir; narrative-only kayıt kabul değildir.
- `py -B tools/evidence-envelope/evidence_envelope_tool.py --envelope
  evidence/<Task-ID>/closure-evidence-envelope.json --repository . --format
  text` non-zero exit verirse task closure kanıtı geçersizdir.
- Tarihsel acceptance replay mevcut `Done` task üzerinde değil, executable
  candidate commit'te repository dışındaki geçici Git worktree'de yapılır.
  Candidate veya gerekli ortam bulunamazsa task `Blocked` kalır; başarı sonucu
  uydurulmaz.

V2 closure protocol ek koşulları:

- V2 kapanışı B subject -> E evidence checkpoint -> F metadata-only final
  zinciridir. B bütün non-evidence owned artifactları ve `Planned`→`InProgress`
  geçişini; E yalnız aktif görev evidence'ını; F yalnız task status satırında
  `InProgress`→`Done` geçişini taşır.
- Git, F'nin bitişik trailer bloğunu tam olarak `Task`, `Gate`,
  `Closure-Subject` ve `Closure-Evidence-Checkpoint` olarak ayrıştırmalıdır.
  Son iki değer B ve E full commit hash'leridir. E, F SHA'sını veya payload hash'ini taşımaz.
- V2, validator, testleri, closure dokümanı ve bu sözleşme dahil B'de değişen her
  owned artifactı hashler. Eksik, stale veya mismatch blob fail-closed olur.
- `--final-commit`, envelope ve kayıtlı her raw output byte'ını yalnız E commit
  tree'sinden okur. Aynı zarf veya kayıtlı raw path worktree'de E blobundan
  farklıysa `WORKTREE_EVIDENCE_SUBSTITUTION` ile non-zero exit verir; worktree
  içeriği kanıt kaynağı olamaz.
- Raw output `evidence/<Task-ID>/` altında kalır; command veya raw transcript
  içinde `Authorization: Bearer <value>` ya da `api key: <value>` secret leakage
  sayılır ve fail-closed olur. Worktree create/remove, exit code, LF raw transcript
  hash ve cleanup sonucu E'de checkpoint edilir.
- `py -B tools/evidence-envelope/evidence_envelope_tool.py --final-commit <F> --repository . --format text`
  task closure öncesi zero exit vermelidir; non-zero exit closure evidence'ı geçersiz kılar.
- `--historical-v0-gov-035`, immutable historical verification ledger'ını eski
  baseline ile gerçek closure blobları karşılaştırarak `STALE_CANDIDATE_COMMIT`
  ve `FINAL_BLOB_HASH_MISMATCH` ile invalid bulmalıdır; eski evidence değişmez.

V3 interrupted remediation closure ek koşulları:

- V3 yalnız `V1-FND-023` için fixed B0
  `fd3344f15c5257b53bf5281ee9129f800c62f0a7` ve fixed interruption
  `479881636c8142c7161f2d5980d37ca2f9b48591` arasında uygulanır; başka task,
  subject veya interruption için generic exception yoktur.
- Verifier B0 parent'ını, `Directory.Build.targets`,
  `tests/Architecture/TestDiscovery/test_solution_test_discovery.py` ve aktif
  task dosyasındaki B0 bloblarını; interruption'ın B0 direct child'ı ve yalnız
  exact `InProgress`→`Blocked` metadata ile exact `Blocker` diff'i olduğunu
  byte/diff/topology olarak doğrular.
- Reentry A, fixed interruption'ın descendant'ı olan geçerli `V0-GOV-060` v2
  finalinin direct child'ı olmalı, yalnız exact `Blocked`→`InProgress` geçişini
  yapmalı ve exact `Blocker` bölümünü kaldırmalıdır. E, A'nın direct child'ı
  olarak yalnız `evidence/V1-FND-023/**` ekler; F, E'nin direct child'ı olarak
  yalnız task `Status: InProgress` satırını `Status: Done` yapar.
- B0'nın iki source artifact blobu V0-GOV-060 finalinde ve A/E/F'de stale veya
  değiştirilmiş olamaz; E zarfı bunların tam SHA-256 kümesini taşır. E
  tree'sindeki envelope/raw bytes, raw hashleri ve worktree-substitution reddi
  v2 kadar zorunludur. F trailer bloğu sırasıyla `Task`, `Gate`,
  `Closure-Subject`, `Closure-Interruption`, `Closure-Reentry` ve
  `Closure-Evidence-Checkpoint` alanlarını full SHA ile taşır.
- `V1-FND-023` `Done` statüsü, ancak fixed v3 F final commit'i üzerinden
  doğrulanırsa kabul edilir: ilgili task-specific v3 verifier sabit
  `_V3_FINAL_COMMIT` commit'inin repository'de mevcut olduğunu ve current
  `HEAD`'in o sabit finalın descendant'ı (ya da kendisi) olduğunu doğrular;
  PlanAudit generic v2 sonucu değil doğrudan task-specific v3 verifier'ı
  çağırır. Bu kontrol V0 gate açık veya kapalıyken zorunludur; task metadata'sı
  tek başına admission değildir.
- Wrong task/subject/interruption, altered B0/interruption byte veya diff,
  non-adjacent A/E/F, evidence dışı E diff, stale B0 blob, final metadata/trailer
  sapması ve worktree substitution deterministic non-zero ile reddedilir.

## Kapanış

İlk doğrulamadan sonra aynı kontroller taze bağlamlı bağımsız denetimde yeniden
çalıştırılır. İkinci çalıştırma sıfır hata üretmeden hiçbir application görevi
başlamaz; Git geçmişinin varlığı veya yokluğu gate yerine geçmez.
`AUDIT_MANIFEST.json`; aktif `plan/`, `docs/`, `evidence/` Markdown dosyaları ile
`AGENTS.md` dosyasını, lint config, runtime/dependency lock'ları, baseline
manifest ve kalıcı audit tool hash'lerini de doğrular. `tmp/plan_audit_original/`
yalnız değişmez başlangıç arşividir ve aktif kapsam değildir.

Mevcut koşullu Markdown sayısı artık sabit değil: `verify-manifest` sayıyı
diskteki `plan/**/*.md` dosyalarından türetir; meal-card provider kararı
eklendikçe `V12-MCD-1xx` dosyaları diskte göründüğünde sayı otomatik artar.
Plan dosyası eklemek/çıkarmak için plan-audit aracında hard-code düzenlemesi
gerekmez. Licensing task dosyaları `NotApplicable` sonucunda da korunur;
bilinmeyen karar için sayı uydurulmaz.
