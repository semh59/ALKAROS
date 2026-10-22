# V1-RMD-695 - Modül sınırı, kayıt ve mimari test

- Task ID: V1-RMD-695
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B025, B026, B048, B049, B050, B090, B091, B093, B094, B095, B097, B099, B100, B101, B104, B105 kapanır. Host'tan modül şemalarına ham yazım modül portlarına taşınır (anonimleştirme durumu, cihaz oturumu son görülme, masa durumu); mimari kenar testi tüm Host ad alanlarını kapsar; kompozisyon testi ömür ve fabrika kayıtlarını denetler; tekrarlı kayıtlar silinir; ölü kod, saat tutarsızlığı ve açılış hataları giderilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-695-module-boundaries-and-composition.md`
- `evidence/V1-RMD-695/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız anonimleştirme yazımı ve açılış
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/** - yalnız kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız modül sınırı ve ölü kod
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/** - yalnız tekrarlı kayıtların silinmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/SettingsModule.cs - yalnız kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/** - yalnız son görülme portu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız masa durumu portu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerData/** - yalnız anonimleştirme portu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/** - yalnız kenar testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json - yalnız yeni uç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-694-cash-and-order-flow-fixes.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Port tanımları, ham SQL'in taşınması, kenar testi kapsamı, tekrar silme, kullanılmayan kod.

## Out of scope

- RefundIntentsModule kaydı (V13-HUG-003, bilinen).

## Dependencies

- V1-RMD-694

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-695/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-696
