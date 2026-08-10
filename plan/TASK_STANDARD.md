# Task Standard

## Kimlik formatı

`<VERSION>-<MODULE>-<NUMBER>`

Sürüm kodları: `V0`, `V1`, `V11`, `V12`, `V13`, `V14`, `V15`, `V20`.
Örnek: `V12-ALC-002`.

## Tek sorumluluk kuralı

Her görev kodu:

- Tek bir kullanıcı davranışı, domain kuralı, şema değişikliği veya dış sistem
  sözleşmesi üretir.
- Başka bir modülün bağımsız iş mantığını aynı değişiklik içine almaz.
- Başarı ve hata yollarını birlikte kapsar; hata yolu ayrı bir özellik sayılmaz.
- Uygulama kodu ekliyorsa aynı davranışın otomatik testini de içerir.
- Migration gerektiriyorsa ileri uygulama ve geri alma kanıtını içerir.
- Dış sistem görevi ise gerçek sandbox/cihaz çıktısı olmadan `Done` olamaz.
- Belirsiz `any`, geniş `catch`, boş handler, sahte adapter veya yalnızca başarılı
  sonucu döndüren stub kabul etmez.
- Dokümantasyonda veya gerçek SDK'da doğrulanmayan API, field, status, provider
  davranışı ya da fallback kabul etmez.
- Ölü/ulaşılamayan kod, speculative future hook, TODO, placeholder, test kapatma,
  warning/analyzer bastırma veya bilinen teknik borç bırakmaz.
- Standard library veya desteklenen framework primitive'i yeterliyse custom
  algorithm ya da gereksiz abstraction üretmez.
- Algorithm ve data structure seçimini correctness, input sınırı, time/space
  complexity, concurrency ve failure davranışıyla kanıtlar; performance iddiası
  benchmark veya profiler çıktısı ister.
- Aynı acceptance ve invariant daha kısa, açık ve test edilebilir kodla
  sağlanıyorsa uzun pattern, katman veya boilerplate kabul etmez.
- Kod, identifier, API contract, log, exception ve test adlarını English tutar.
- Kendi Markdown dosyasında yaşar; iki görev aynı dosyada tanımlanmaz.
- Tek kişiye atanır; ortak sahiplik veya eşzamanlı iki uygulayıcı kabul edilmez.
- Dışarı açılan davranış için aynı owned surface içinde versioned API/event
  contract ve contract testini teslim eder.
- `ASSUMPTION_POLICY.md` içindeki kanıt sınıflarından en az birine dayanır.

## Metadata alanları

- `Task ID`: Dosya adı ve ilk başlıktaki tekil, değişmez kod.
- `Status`: `Planned`, `InProgress`, `Blocked`, `NotApplicable` veya `Done`.
- `Assignee`: Aynı anda tam olarak bir kişi; `InProgress`, `NotApplicable` ve `Done` için gerçek kimlik.
- `Work type`: `decision`, `documentation`, `implementation`, `integration`,
  `release`, `release gate` veya `validation`.
- `Surface state`: Kod iskelesi kurulana kadar `Planned`, gerçek yol
  doğrulandıktan sonra `Existing`.

`NotApplicable` yalnız koşullu bir task için, tamamlanmış ve tarihli decision
kanıtı capability/policy'nin uygulanmadığını açıkça gösteriyorsa kullanılır.
Dosya silinmez. `Acceptance evidence`, karar kimliği, tarih, approver ve neden
kod/artifact üretilmediğini kaydeder. `NotApplicable` bir `Blocked` kısaltması
veya başarısız test feragati değildir.

Bir dependency yalnız `Done` ile kapanır. Geçerli `NotApplicable`, ancak consumer
task'ın `Acceptance evidence` bölümü bu sonucu adıyla ele alıyor ve kalan
davranışın nasıl doğrulanacağını belirtiyorsa terminal dependency sayılır.
`Blocked`, `Planned` veya kanıtsız `NotApplicable` dependency'yi kapatmaz.

`Done` statüsündeki bir task'ın bütün doğrudan ve transitive task dependency'leri
de `Done` olmalıdır. Bu kuralda `NotApplicable` terminal dependency değildir;
consumer task ancak kendi sonucu `Done` olmadan bekler. Plan denetimi doğrudan
ihlal için `DONE_DEPENDENCY_NOT_FINAL`, ancestor ihlali için
`DONE_DEPENDENCY_TRANSITIVE_NOT_FINAL` üretir.

Bu beş metadata alanı görev başlığının hemen altında ve yukarıdaki sırada yer
alır. `Task ID` yalnız başlıktan türetilmiş kabul edilmez; metadata satırı da
zorunludur.

## Decision görevi çıktısı

Her `Work type: decision` görevi tek decision record üretir. Bu kayıt kaynakları,
erişim tarihlerini, onaylayanı, seçilen sonucu, reddedilen alternatifleri ve
etkilenen kesin task kimliklerini içerir. Örnek hesap veya matrix aynı record'un
eki olabilir; bağımsız ikinci karar üretilemez.

## Provider-specific görev üretimi

`V0-MCD-001` approved provider listesi ve legal provider code üretmeden
provider-specific görev oluşturulmaz. Sonuç üretildiğinde provider'lar legal
code'a göre sıralanır; aynı sıra için tek `V12-MCD-1xx` adapter görevi ve tek
`V20-INT-1xx` certification görevi açılır. Bir dosya birden fazla provider
uygulayamaz veya sertifikalandıramaz.

## Bölme testi

Bir görev açıklamasında birbirinden bağımsız iki fiil varsa görev bölünür. Aynı
transaction veya aynı invariant içinde zorunlu olarak birlikte değişen adımlar
tek görev kalabilir.

## Codex yürütme sözleşmesi

- Kodlama isteği tam olarak bir `Task ID` belirtir; aynı diff içinde ikinci iş
  alınmaz.
- Codex, dependency ve gate kanıtını okumadan `Status: InProgress` yapamaz.
- `Assignee` gerçek Codex task/thread kimliğidir; genel AI etiketi değildir.
- `Owned surface`, yazma allowlist'idir; açıklama veya yaklaşık klasör önerisi
  değildir.
- `V1-FND-001` tarafından sahiplenilen solution/project/build dosyaları global
  reserved surface'tir; feature task klasör wildcard'ı bu dosyalara yazma izni vermez.
- Aktif görev dosyasında yalnız `Status` ve `Assignee` yürütme sırasında
  değişebilir. `Blocked` ile `Planned` veya `InProgress` arasındaki geçişte zorunlu
  `Blocker` bölümü de eklenebilir veya silinebilir. Scope değişikliği ayrı plan
  değişikliğidir.
- Her görev `evidence/<Task-ID>/**` altında yeniden üretilebilir kabul kanıtı
  bırakabilir; bu izin başka görev kanıtlarına erişim vermez.
- Allowlist dışı ihtiyaç, kullanıcıya kesin path ve gerekçeyle blocker olarak
  bildirilir; görev kendiliğinden genişletilmez.
- Repository-wide formatter, dependency upgrade veya unrelated cleanup ancak
  bunları açıkça sahiplenen ayrı görevde yapılabilir.
- Compile, static analysis ve acceptance testleri doğrulanmadan; kullanılmayan
  kod, dependency, public API veya test helper varken görev `Done` yapılamaz.
- `V1-FND-001`, `V1-FND-010`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`,
  `V1-SEC-002`, `V1-FND-002` ve `V1-FND-006` bu sırayla `Done` olmadan başka
  application görevi başlatılamaz.

## Zorunlu görev dosyası bölümleri

Sıra değiştirilemez:

1. `Source basis`
2. `Goal`
3. `Owned surface`
4. `In scope`
5. `Out of scope`
6. `Dependencies`
7. `Blocker` - yalnız `Blocked` görevlerde
8. `Deliverables`
9. `Acceptance evidence`
10. `Handoff`

`Dependencies` ve `Handoff` satırları yalnız görev ID'si, sabit gate ID'si veya
`None` içerir. “All tasks”, “owners” ya da yorum gerektiren serbest metin
kullanılmaz.
