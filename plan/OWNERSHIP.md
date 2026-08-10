# Ownership Boundaries

## Tek sahip

- Her görev dosyasının aynı anda tam olarak bir assignee'si olur.
- Bir kişi bir görev `InProgress` iken ikinci görevi `InProgress` yapmaz.
- Pair review yapılabilir; üretim değişikliğinin sahibi yine tek kişidir.

## Kod yüzeyi

- Görev yalnızca kendi `Owned surface` alanını değiştirir.
- Başka bir görevin alanına ihtiyaç duyulursa mevcut görev genişletilmez; yeni
  bağımlılık veya ayrı integration görevi açılır.
- Module composition root, shared kernel ve global migration sırası yalnızca
  bunları açıkça sahiplenen görev tarafından değiştirilir.
- `V1-FND-001` içindeki exact solution, project ve root build/config yolları
  reserved surface'tir. Feature task'larının parent wildcard'ı bu dosyaları kapsamaz.
- Her şema görevi kendi migration dosyasını üretir. Önceki migration dosyası
  sonradan yeniden yazılmaz.

## Review sınırı

Reviewer kod sahipliğini devralmaz. Review; invariant, test kanıtı, migration ve
hata davranışını doğrular. Kabul sonrası değişiklik gerekiyorsa yeni görev kodu
açılır.

## Çakışma kuralı

İki görev aynı production dosyasını değiştirmek zorundaysa paralel değildir.
Bağımlılık sırasına alınır veya ortak dosyayı sahiplenen tek bir integration
görevine bağlanır.

## Codex write-set sınırı

ChatGPT Codex ile yürütülen her kodlama oturumu tam olarak bir `Task ID` ile
başlar. Yazma yetkisi yalnız şu birleşimdir:

- Aktif görevin açık `Owned surface` yolları.
- Yalnız `Status` ve `Assignee` güncellemesi için aktif görev dosyası.
- Yeniden üretilebilir kanıtlar için `evidence/<Task-ID>/**`.

Okunabilen bir dosya yazılabilir sayılmaz. Parent directory, benzer path,
generated file, lockfile, project/solution dosyası, shared component ve global
configuration örtük izin üretmez. Rename işleminde eski ve yeni yol ayrı ayrı
izinli olmalıdır.

Codex, ilk yazmadan önce mevcut Git değişikliklerini ve görev allowlist'ini
kaydeder. Kapanışta staged, unstaged, untracked, deleted ve renamed yolların
tamamını allowlist ile karşılaştırır. Tek bir kapsam dışı yol görevi başarısız
yapar; dosya düzeltilerek gizlice kapsam içine alınamaz.

Kapsam dışı değişiklik gerektiğinde yürütme durur. Yeni dependency veya tek
sahipli integration görevi planlanıp doğrulanmadan mevcut görev genişletilemez.
Bu zorunluluk repository kökündeki `AGENTS.md` ve `V1-FND-003` otomatik kapısıyla
birlikte uygulanır.
