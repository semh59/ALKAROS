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
- Kendi Markdown dosyasında yaşar; iki görev aynı dosyada tanımlanmaz.
- Tek kişiye atanır; ortak sahiplik veya eşzamanlı iki uygulayıcı kabul edilmez.
- Dışarı açılan davranış için aynı owned surface içinde versioned API/event
  contract ve contract testini teslim eder.
- `ASSUMPTION_POLICY.md` içindeki kanıt sınıflarından en az birine dayanır.

## Görev satırı alanları

- `Task ID`: Tekil ve değişmez kod.
- `Single responsibility`: Görevin üreteceği tek sonuç.
- `Depends on`: Başlamadan önce kapanması gereken görevler.
- `Acceptance evidence`: Tamamlanmayı kanıtlayan ölçülebilir çıktı.
- `Status`: `Planned`, `InProgress`, `Blocked` veya `Done`.

## Bölme testi

Bir görev açıklamasında birbirinden bağımsız iki fiil varsa görev bölünür. Aynı
transaction veya aynı invariant içinde zorunlu olarak birlikte değişen adımlar
tek görev kalabilir.

## Zorunlu görev dosyası bölümleri

Her görev dosyasında `Goal`, `Owned surface`, `In scope`, `Out of scope`,
`Dependencies`, `Deliverables`, `Acceptance evidence` ve `Handoff` bölümleri
eksiksiz bulunur.
