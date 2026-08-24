# V1-RMD-006 doğrulama kanıtı

- Tarih: 2026-08-24
- Repository: `D:\PROJECT\ALKAROS`
- Task: `V1-RMD-006`

## Build ve test

- `dotnet restore ALKAROS.slnx --locked-mode`: exit `0`.
- `dotnet build ALKAROS.slnx --no-restore`: exit `0`, uyarı `0`, hata `0`.
- Dual-screen ve manifest odaklı .NET testleri: exit `0`, `21/21` test geçti.
- `pnpm install --frozen-lockfile`: exit `0`.
- `pnpm typecheck`: exit `0`.
- `pnpm test`: exit `0`, `2/2` test geçti.
- `pnpm build`: exit `0`.
- Vite production çıktısı: JavaScript `264.63 kB`, gzip `78.88 kB`; CSS `17.04 kB`, gzip `4.66 kB`.

Odaklı .NET test komutu:

```text
dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --no-restore
  --filter "FullyQualifiedName~DualScreen|FullyQualifiedName~ManifestTests"
  --environment DOTNET_ROLL_FORWARD=Major
```

## PostgreSQL 18 migration

- Test motoru: PostgreSQL `18.6`, geçici Docker container.
- Mevcut V1 migration zinciri boş `alkaros` veritabanına ileri yönde uygulandı: exit `0`.
- `038-dual-screen-pos.down.sql`: exit `0`.
- `038-dual-screen-pos.up.sql`: exit `0`.
- Down sonrasında tekrar up: exit `0`.
- Doğrulanan tablolar: `display_sessions`, `pairing_requests`, `terminals`.
- Açık pairing kodu indeksi `code_hash` üzerinde unique ve `consumed_at IS NULL` koşulludur.

## Yetki ve hata yolları

Gerçek HTTP istekleriyle alınan durum kodları:

```text
login=200
approve=204
complete=200
display-mutation=403
wrong-display=403
revoke=200
revoked-session=401
```

Pairing, onay ve complete rate-limit politikaları birbirinden ayrıldı. Normal display polling artık cashier onay
kotasıyla çakışmıyor. Pairing kodları yalnız hash olarak tutuluyor ve açık kod çakışmaları veritabanı unique indeksiyle
engelleniyor.

## Docker canlı ortam

- Docker Client ve Engine: `29.7.2`.
- Container: `alkaros-live-pg18`, image `postgres:18-alpine`, port `55438`.
- Bütün `001..038` migration zinciri yeni `alkaros` veritabanına uygulandı: exit `0`.
- Uygulama `http://localhost:5080/` üzerinde gerçek PostgreSQL bağlantısıyla çalışıyor.
- `/health/ready` sonucu `200`; kasa başlığındaki durum bu endpoint'in gerçek sonucunu gösteriyor.
- Sentetik canlı test catalog kaydı PostgreSQL'e yazıldı; frontend içine ürün veya sipariş fixture'ı eklenmedi.

## Repository kapıları

- `python -B tools/plan-audit/plan_audit_tool.py validate`: exit `0`, hata `0`, uyarı `0`.
- `python -B tools/task-scope/task_scope_tool.py --task-id V1-RMD-006 --format text`: exit `0`.
- `git diff --check`: exit `0`.
- Task-owned Markdown yollarında `markdownlint-cli2@0.23.2 --no-globs`: exit `0`, ihlal `0`.
- Kök `markdownlint-cli2@0.23.2`: exit `1`, bu görevden önce var olan üç kapsam dışı dosyada yedi ihlal.

Kalan lint ihlalleri:

- `evidence/V1-RMD-004/validation.md`: üç adet `MD013`.
- `evidence/V1-RMD-005/validation.md`: üç adet `MD013`.
- `plan/GATES.md`: bir adet `MD022`.

Bu üç yol `V1-RMD-006` owned surface alanında değildir. Bu nedenle kapsam dışı dosyalar değiştirilmedi ve görev
yanlış biçimde `Done` durumuna alınmadı.

## SHA-256

```text
147FF9DBD19178BFDFA316705BF31C4642DCD6A7281CF5F4DD03758A7C870023 pnpm-lock.yaml
FDBB02D00CA41E0A3EC3E8D627B06E2C642998F01D2525C9DE3C86A0067874AA ALKAROS.Host.csproj
0FCC0DC4DFF33EA44793004F364CD91EB8F5014C60BA82934F264D60E01DF8F6 038-dual-screen-pos.up.sql
0766FDAF632E91027CBFA27C0AC78DF4A740477417F76E96DC1D5D5A10A14B4A 038-dual-screen-pos.down.sql
6C5A3618DB9BBADE1BEF5B5D82595C54ADA9161E9AF760FAE002D11F8E89898C App.tsx
86B08C496F96855A0336E6538A3AADA63239001049F1BBF3F4699946B4F57188 styles.css
AC755A281E9E35CEFF45F33FBE037056CAD49102ECBCA23644AB23AE60DCF4C2 DualScreenStore.cs
```
