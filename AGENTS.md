# ALKAROS Codex Execution Contract

Bu dosya, repository içinde kod yazan bütün ChatGPT Codex oturumları için zorunludur. Kullanıcı açıkça plan değişikliği
istemeden hiçbir Codex oturumu bu kuralları gevşetemez veya görev kapsamını sohbet içinde genişletemez.

## Tek görev sınırı

- Her kodlama oturumu başlamadan önce tam olarak bir `Task ID` seçilir.
- Bir oturum aynı anda ikinci bir görevi `InProgress` yapamaz.
- Seçilen görev dosyası, `plan/TASK_STANDARD.md`, `plan/OWNERSHIP.md`, ilgili gate ve bütün dependency görevleri yazma
  işleminden önce okunur.
- Görev `Planned` değilse veya dependency/gate kanıtı tamamlanmamışsa kodlama başlamaz.
- `Assignee`, yazmadan önce gerçek Codex task/thread kimliğiyle güncellenir; genel `Codex`, `AI` veya ortak sahiplik
  kabul edilmez.

## Yazılabilir yüzey

Bir Codex oturumu yalnız şu yolları değiştirebilir:

1. Aktif görevde açıkça listelenen `Owned surface` yolları.
2. Yalnız aktif görevin `Status` ve `Assignee` metadata satırları için kendi görev Markdown dosyası.
3. Komut çıktısı, test raporu ve hash gibi yeniden üretilebilir kanıtlar için `evidence/<Task-ID>/**`.

Okuma erişimi yazma yetkisi değildir. Parent directory, benzer adlı dosya, solution/project dosyası, lockfile, shared
component, composition root, global configuration, önceki migration veya başka görev dosyası örtük olarak kapsamda
sayılmaz. Rename işleminde hem eski hem yeni yol allowlist içinde olmalıdır. Generated file da aynı sınır içindedir.

## Zorunlu preflight

Codex ilk yazmadan önce:

- Repository root ve aktif `Task ID` değerini bildirir.
- `git status --short` ve `git diff --name-only` ile başlangıç write-set snapshot'ını kaydeder.
- Görevdeki `Owned surface` değerlerinden kesin write allowlist üretir.
- Mevcut kullanıcı değişikliklerini ayırır; bunları değiştirmez, silmez, taşımaz veya geri almaz.
- Gerekli gerçek kaynak, decision record, sandbox/device kanıtı ya da secret erişimi yoksa durur; stub veya varsayılan
  davranış üretmez.

Git deposu yoksa uygulama kodlaması başlamaz. Yalnız kullanıcı tarafından açıkça istenen plan ve repository hazırlık
işleri bu kuralın dışındadır.

## Kapsam dışına çıkma yasağı

- Kapsam dışı dosya gereksinimi ortaya çıkarsa Codex yazmayı durdurur ve kesin yolu, gerekçeyi ve gerekli yeni
  dependency/integration görevini bildirir.
- Aktif görev sırasında `Goal`, `Owned surface`, `In scope`, `Out of scope`, `Dependencies`, `Deliverables`,
  `Acceptance evidence` veya `Handoff` genişletilemez.
- İlgisiz refactor, cleanup, dependency upgrade, repository-wide formatter veya toplu rename çalıştırılamaz.
- Başka görevin production yüzeyi, testi, migration'ı veya plan dosyası değiştirilemez.
- TODO, placeholder, mock-success adapter, boş handler, sessiz catch/pass ve kanıtsız fallback bırakılamaz.
- Kullanıcı yeni kapsam isterse önce plan değişikliği ayrı olarak yazılır ve doğrulanır; aynı kodlama diff'ine gizlenmez.

## Kod doğruluk ve sadelik sözleşmesi

- Kod, identifier, API contract, log, exception ve test adı English olur. Kullanıcıya açıklama ve plan metni Turkish
  kalır.
- Dokümantasyonda veya yüklü SDK'da doğrulanmayan API, field, status, provider davranışı ya da fallback yazılamaz.
- Hayali/varsayımsal akış, kullanılmayan future hook, ölü veya ulaşılamayan kod, stub, TODO, placeholder, mock-success,
  boş handler ve sessiz catch/pass yasaktır.
- Bilinen teknik borç bırakılamaz; test kapatma, warning/analyzer bastırma, gereksiz dependency ve kanıtsız temporary
  workaround kabul edilmez.
- Önce standard library ve framework'ün desteklenen güvenli primitive'leri değerlendirilir. Custom algorithm ancak
  correctness, complexity veya ölçülmüş performance ihtiyacı bunu gerektiriyorsa yazılır.
- Aynı kabul koşulu ve invariant daha kısa, açık ve test edilebilir kodla sağlanıyorsa uzun abstraction, pattern veya
  boilerplate reddedilir. Satır sayısı hedef değildir; gereksiz satır ve gereksiz katman yasaktır.
- Algorithm ve data structure seçimi input sınırı, correctness, time/space complexity, concurrency ve failure
  davranışıyla gerekçelendirilir. Performance iddiası benchmark veya profiler kanıtı olmadan yapılamaz.
- Yeni davranış compile, static analysis ve task acceptance testleriyle doğrulanmadan `Done` olamaz. Kullanılmayan kod,
  dependency, public API veya test helper diff içinde bırakılamaz.

## Kapanış kapısı

Görev `Done` yapılmadan önce:

- Başlangıç snapshot'ına göre staged, unstaged, untracked, deleted ve renamed yolların tamamı allowlist ile
  karşılaştırılır.
- Allowlist dışındaki tek bir değişiklik bile görevi başarısız yapar.
- Görevdeki acceptance komutları ve ilgili testler gerçek exit code ile çalıştırılır.
- Migration varsa ileri/geri kanıtı; dış entegrasyon varsa gerçek sandbox/device transcript'i bulunur.
- `evidence/<Task-ID>/**` altında komut, exit code, ilgili hash ve sonuç kaydedilir.
- Final cevap değişen yolları, çalıştırılan kontrolleri ve kalan blocker'ları açıkça listeler.

`V1-FND-001`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`, `V1-SEC-002`, `V1-FND-002` ve `V1-FND-006`
bu sırayla gerçek kanıtla `Done` olmadan başka hiçbir application görevi `InProgress` yapılamaz. `V1-FND-001`
tarafından sahiplenilen exact solution/project/build dosyaları reserved surface'tir; feature task wildcard'ı bu
dosyalara yazma izni vermez.
