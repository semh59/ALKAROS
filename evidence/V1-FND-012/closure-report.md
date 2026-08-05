# V1-FND-012 doğrulama kaydı

- Task ID: `V1-FND-012`
- Tarih: 2026-08-02
- Sonuç: Başarılı

## Uygulanan düzeltme

Çalışma zamanı migration manifesti yalnız diskte bulunan tam `up.sql`/`down.sql`
çiftlerini içerir: `001`, `002`, `003` ve `005`.

## Çalıştırılan kontroller

- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --nologo`
  - Exit code: `0`
  - Sonuç: `62 passed, 0 failed` (2026-08-05 taze doğrulama)
- Host çalışma zamanı migration kompozisyonu disposable PostgreSQL 18 veritabanında
  çalıştırıldı.
  - Exit code: `0`
  - Sonuç: Dört migration uygulandı; `idempotency_keys`, `inbox_messages`,
    `outbox_messages` ve `identity.users` tabloları doğrulandı.

Doğrulama sırasında erişim değerleri yalnız process environment üzerinden alındı;
bu kanıt dosyasında secret veya bağlantı değeri tutulmaz.
