# V1-RMD-398 probe'ları — çalıştırma

`StockFlowProbes/` bir xUnit projesidir; `ALKAROS.slnx` içinde değildir ve CI tarafından derlenmez. Her probe
sistemin **yapması gereken** davranışı doğrular: başarısız probe bir bulgudur, geçen probe ilgili matris
hücresini "sağlam" olarak kapatır.

Gereken ortam: .NET SDK 10.0.302 + .NET 8 shared runtime (test host net8.0), PostgreSQL 18 ve `psql`.

```sh
export ALKAROS_TEST_PG_HOST=127.0.0.1 ALKAROS_TEST_PG_PORT=5432 \
       ALKAROS_TEST_PG_USER=postgres ALKAROS_TEST_PG_PASSWORD=postgres
# Kilitli restore master HEAD'de bozuk (bkz. V1-RMD-393 REPORT.md F-02; V1-RMD-394 ile düzeltildi); bu yüzden kilitsiz restore:
dotnet restore evidence/V1-RMD-398/probes/StockFlowProbes -p:RestoreLockedMode=false
dotnet test evidence/V1-RMD-398/probes/StockFlowProbes -c Release --no-restore --logger "console;verbosity=normal"
```

Kilitsiz restore `packages.lock.json` dosyalarını yeniden yazar; bu yüzden komutlar deponun bir kopyasında
(`git clone <repo> /work`) çalıştırılmalıdır. Denetimdeki gerçek koşu çıktısı: `run-*.log`.
