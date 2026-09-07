# V11-PUR-002 - Implement Supplier master data

- Task ID: V11-PUR-002
- Status: Done
- Assignee: f2f02a8c-1e37-451a-b2c8-e6543e3fdf89
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:III.15

## Goal

KVKK/veri minimizasyon kuralları kapsamında minimum tedarikçi kimliğini, vergi/iletişim verilerini, aktif durumu ve
benzersizliği uygulayın.

## Owned surface

- `src/Modules/Purchasing/Suppliers/**`, `tests/Modules/Purchasing/Suppliers/**`,
  `database/migrations/V11/V11-PUR-002/**`
- `src/Modules/Purchasing/ALKAROS.Purchasing.csproj`, `src/Modules/Purchasing/packages.lock.json`, `tests/Modules/Purchasing/Suppliers/ALKAROS.Purchasing.Suppliers.Tests.csproj`
- Kapsam genişletme onayı (2026-09-06 kullanıcı talimatı): bu task'ın yeni projelerinin `ALKAROS.slnx` ve `build/project-manifest.json` içine kaydı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Tedarikçi kodu/adı, vergi kimliği, iletişim alanları, aktif durum ve erişim kontrolü.

## Out of scope

- Tedarikçiye ödenecek defter ve gelen invoice eşleştirmesi.

## Dependencies

- V1-IAM-002
- V0-CMP-003

## Deliverables

- `src/Modules/Purchasing/Suppliers/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yinelenen kod/vergi politikası uygulanıyor; etkin olmayan tedarikçi yeni order'yi reddediyor; korunan alanlar erişim
  kurallarına uyar.

## Handoff

- V11-PUR-001
- V14-PUR-001
