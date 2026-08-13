# V1-FND-001 / V1-FND-004 — Kusur 1 Kapanış Kanıtı (2026-08-03)

Kapsam: EXECUTION_READY_PLAN.md Aşama 2, satır 1 — "Module registrations
compose sonunda kayboluyordu; Host henüz bunları DI adapter'a uygulamıyor."

## Değişen yollar

| Dosya | Değişiklik |
| --- | --- |
| `src/Host/Composition/Modules/ModuleRegistry.cs` | `ComposeRoot(IEnumerable<Type>)` eklendi; mevcut `Compose` bunun üzerine kuruldu; `ModuleCompositionRoot.Services` DI adapter'ın girdisi |
| `src/Host/Composition/HostComposition.cs` | `ComposeModules(TextWriter, IEnumerable<Type>? = null)`: discovery → `ComposeRoot` → `ServiceCollection.AddRegistration` (instance/transient/singleton) → `BuildServiceProvider` → fail-closed catch. `Run` içinde `using var services = ComposeModules(output)` + `ComposeRegisteredServices(output)` validation |
| `Directory.Packages.props` | `Microsoft.Extensions.DependencyInjection` 8.0.1 paket versiyonu |
| `src/Host/ALKAROS.Host.csproj` | `Microsoft.Extensions.DependencyInjection` PackageReference |
| `src/Host/packages.lock.json` vb. lock dosyaları | restore sonrası güncel |
| `tests/Host/MigrationComposition/Composition/HostServiceRegistrationTests.cs` | Yeni test dosyası: instance+type+transient resolution, throwing module fail-closed, zero-services raporlama |

## Davranış

- Module'lerin `RegisterServices` ile sağladığı registration'lar (instance,
  transient, singleton) gerçek `ServiceCollection`'a uygulanır;
  `BuildServiceProvider` ile çözümlenebilir hale gelir.
- Bir module `ComposeRoot` sırasında hata fırlatırsa fail-closed davranış
  korunur (hata raporlanır, kısmi servis kurulmaz).
- Hiç servis kaydı olmayan module'ler açıkça raporlanır.

## Test kanıtı

Identity suite hariç Host suite koşusu (tam suite içinden):

```console
Başarılı!  - Başarısız:     0, Başarılı:    60, Atlanan:     0, Toplam:    60, Süre: 14 s - ALKAROS.Host.Tests.dll (net8.0)
```

Host 57 → 60 test (3 yeni DI adapter testi).

Build:

```console
Oluşturma başarılı oldu.
    0 Uyarı
    0 Hata
EXIT=0
```

## Kapanış doğrulaması

- Kapanış ölçütü: "Host concrete registration'ı uygular; architecture testi
  geçer." — DI adapter testleri 3/3 geçti, Architecture 5/5, Host 60/60.
- Write-set yalnız yukarıdaki yollardadır. Commit bu kanıt dosyalarıyla birlikte push edildi (2026-08-03).
