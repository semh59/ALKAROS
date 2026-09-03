# V1-IAM-019 - Grant Request Engine And Event

- Task ID: V1-IAM-019
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Asenkron yetki isteği motoru: `grant` sınıfı bir eylem, değişmez bir `authorization_grants` isteği yaratır; çözüm sırası politika, devir ve yönetici kararıdır; her sonlanış tek bir append-only satırdır (isteyen, onaylayan ya da politika, `policy_path`, `reason_code`, para farkı) ve `reporting` projeksiyonunu besler.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-019-grant-request-engine-and-event.md`
- `database/migrations/V1/V1-IAM-019/**`
- `src/Modules/Identity/Authorization/019/**`
- `tests/Modules/Identity/Authorization/019/**`
- `evidence/V1-IAM-019/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-018

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; bu görevin yeni testleriyle ilgili `dotnet test` süzgeci geçer.
- Veri değişiyorsa migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: garson kendi çekinde ikram dener ve istek oluşur; başka garsonun çekinde denerse yöneticiye ulaşmadan kendiliğinden reddedilir; raporlama tarafında gün, garson ve gerekçe kırılımında ikram tutarı satırı üretilir.

## Handoff

- V1-IAM-020
- V1-IAM-023
