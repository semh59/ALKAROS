# V1-SEC-003 dogrulama kaydi

- Task ID: `V1-SEC-003`
- Tarih: 2026-08-02
- Sonuc: Basarili

## Uygulanan duzeltme

Host artik `--db-password` argumentini kabul etmez. Parola yalniz
`ALKAROS_DB_PASSWORD` environment degiskeninden okunur; eksik veya bos deger
startup failure sonucu verir.

## Kontroller

- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --nologo`
  - Exit code: `0`
  - Sonuc: `48 passed, 0 failed`
- Command-line password rejection testi, verilen secret degerinin stderr'e
  yazilmadigini dogrular.
