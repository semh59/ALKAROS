# V1-RMD-393 probe'ları — çalıştırma

`MoneyFlowProbes/` bir xUnit projesidir; `ALKAROS.slnx` içinde değildir ve CI tarafından derlenmez. Her probe
sistemin **yapması gereken** davranışı doğrular: başarısız probe bir bulgudur, geçen probe ilgili matris
hücresini "sağlam" olarak kapatır.

Gereken ortam: .NET SDK 10.0.302 + .NET 8 shared runtime (test host net8.0), PostgreSQL 18 ve `psql`.

```sh
export ALKAROS_TEST_PG_HOST=127.0.0.1 ALKAROS_TEST_PG_PORT=5432 \
       ALKAROS_TEST_PG_USER=postgres ALKAROS_TEST_PG_PASSWORD=postgres
# Kilitli restore master HEAD'de bozuk (bkz. REPORT.md F-01); bu yüzden kilitsiz restore:
dotnet restore evidence/V1-RMD-393/probes/MoneyFlowProbes -p:RestoreLockedMode=false
dotnet test evidence/V1-RMD-393/probes/MoneyFlowProbes -c Release --no-restore --logger "console;verbosity=normal"
```

Kilitsiz restore `packages.lock.json` dosyalarını yeniden yazar; bu yüzden komutlar deponun bir kopyasında
(`git clone <repo> /work`) çalıştırılmalıdır. Denetimdeki gerçek koşu çıktısı: `run-*.log`.
