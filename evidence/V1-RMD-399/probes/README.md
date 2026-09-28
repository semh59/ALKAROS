# V1-RMD-399 probe'ları — çalıştırma

`AuthorizationProbes/` bir xUnit projesidir; `ALKAROS.slnx` içinde değildir ve CI tarafından derlenmez. Her probe
sistemin **yapması gereken** yetkilendirme davranışını doğrular: başarısız probe bir bulgudur, geçen probe ilgili
matris hücresini (`../coverage-matrix.md`) "sağlam" olarak kapatır.

Probe'lar gerçek `DualScreenApplication.Build` sunucusunu ve `database/MigrationComposition/order.json` içindeki
bütün migration'ları uygulanmış gerçek PostgreSQL'i kullanır. Giriş gerektiren probe'lar gerçek
`/api/v1/auth/login` ucundan geçer (üretim gücünde PBKDF2 parola özeti).

`D1`/`D2` probe'ları uygulamanın uç nokta kaynağındaki (`EndpointDataSource`) bütün rotaları dolaşır:

- `D1`: oturumsuz istek; kamusal olmayan her rota 401/403 dönmeli.
- `D2`: izinsiz bir rolün kasiyer + yönetici çerezleriyle her mutasyon rotası; 401/403 dönmeli. Tasarım gereği
  yalnız oturum isteyen rotalar (grant sınıfı ve kişinin kendi kaynağına dokunan rotalar) kaynağıyla birlikte
  probe içinde listelenmiştir.
- JSON `{}` gövdesinin ulaşamadığı rotalar (çok parçalı gövde, zorunlu sorgu parametresi, izin kontrolünden önce
  doğrulama) `D1SecondPass` / `D2SecondPass` ile iyi biçimlenmiş istekle ayrıca sınanır.

Gereken ortam: .NET SDK 10.0.302 + .NET 8 shared runtime (test host net8.0), PostgreSQL 18.

```sh
export ALKAROS_TEST_PG_HOST=127.0.0.1 ALKAROS_TEST_PG_PORT=5432 \
       ALKAROS_TEST_PG_USER=postgres ALKAROS_TEST_PG_PASSWORD=postgres
dotnet restore evidence/V1-RMD-399/probes/AuthorizationProbes -p:RestoreLockedMode=false
dotnet test evidence/V1-RMD-399/probes/AuthorizationProbes -c Release --no-restore --logger "console;verbosity=detailed"
```

Kilitsiz restore `packages.lock.json` dosyalarını yeniden yazar; komutlar deponun bir kopyasında
(`git clone <repo> /work`) çalıştırılmalıdır. Denetimdeki gerçek koşu çıktısı: `run-1-blind.log`.
