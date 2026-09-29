# V1-RMD-396 — doğrulama

## Test

`dotnet test tests/Host/Experience/Composition -c Release` → 10/10 geçti (öncesinde
`ServeContainerResolvesEveryModuleServiceFromTheModuleCatalog` kırmızıydı).

## Mutasyon — görev dosyasındaki tanımın düzeltilmesi

Görev dosyası mutasyonu "`OnlineOrderingModule` içindeki Trendyol consumer kaydı kaldırıldığında test kırmızıya
döner" diye tarif ediyor. Bu **yanlış tasarlanmış**: test beklenen listeyi de aynı modül kataloğundan kurduğu için
kayıt modülden kaldırılınca hem beklenen hem çözülen listeden birlikte düşer, test yeşil kalır. Görev
`InProgress` iken görev metni değiştirilemeyeceği için doğru mutasyon burada kayıt altına alındı:

- Mutasyon: `HostComposition.AddRegistration` factory dalında
  `if (descriptor.ServiceType == typeof(IIntegrationEventConsumer)) return;` — yani serve host, factory ile
  kaydedilmiş consumer'ı modül kataloğundan kopyalamayı atlar (testin korumak için var olduğu gerçek hata sınıfı).
- Sonuç: test **kırmızı** (1 başarısız / 9 geçti). Mutasyon geri alınınca 10/10 yeşil.
- Mutasyon yalnız denetim konteynerindeki kopyada uygulandı; depoda hiçbir üretim dosyası değişmedi.
