# V1-RMD-868 - Billing ve Kitchen projeleri Npgsql paketine doğrudan bağlanır

- Task ID: V1-RMD-868
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9/F0082 ve aynı hatanın ikinci örneği: src/Modules/Billing ve src/Modules/Kitchen kodu `using Npgsql` ile Npgsql tipleri kullanır (PostgresBillRepository, PostgresBillTerminalOwnerRepository, TableEventBillConsumer, Kitchen depoları) ama proje dosyalarında Npgsql PackageReference'ı yoktur; derleme yalnız başka bir projenin (Orders) geçişli bağımlılığı sayesinde çalışır. Orders paketi bırakırsa ya da sürümü ayrışırsa bu iki modül sessizce kırılır ya da yanlış sürümle derlenir. Diğer tüm src projelerinde (taranarak doğrulandı) kullanan proje paketi doğrudan bildirir. İki projeye merkezi sürüm yönetimiyle `PackageReference Include="Npgsql"` eklenir; kilit dosyaları yeniden üretilir.

## Owned surface

- `evidence/V1-RMD-868/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Billing/ALKAROS.Billing.csproj — Npgsql doğrudan referansı.
  - src/Modules/Billing/packages.lock.json — yeniden üretilen kilit.
  - src/Modules/Kitchen/ALKAROS.Kitchen.csproj — Npgsql doğrudan referansı.
  - src/Modules/Kitchen/packages.lock.json — yeniden üretilen kilit.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. İki PackageReference (sürüm Directory.Packages.props'tan gelir, yeni sürüm eklenmez).
2. Kilit dosyalarının `dotnet restore --force-evaluate` ile yeniden üretilmesi; yalnız bu iki dosya değişir.
3. Kanıt: önce/sonra `dotnet list package --include-transitive` çıktısında Npgsql'in "Top-level" olduğu; Billing ve Kitchen derlemesi ve Billing testleri exit 0.

## Out of scope

- Paket sürümü yükseltmek; başka proje dosyaları.

## Dependencies

- None

## Acceptance evidence

1. Billing ve Kitchen projeleri derlenir (exit 0), ilgili modül testleri geçer; çıktılar `evidence/V1-RMD-868/`.
2. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
