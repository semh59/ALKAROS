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

