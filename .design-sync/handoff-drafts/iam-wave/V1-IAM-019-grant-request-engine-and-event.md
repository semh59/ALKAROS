# V1-IAM-019 - Grant Request Engine And Event

- Task ID: V1-IAM-019
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Asenkron yetki-istegi motoru: bir `grant` eylemi degismez `authorization_grants` istegi yaratir; cozum sirasi politika -> devir -> yonetici karari; her terminal durum tek append-only satir (requester, approver/policy, `policy_path`, `reason_code`, para farki) ve `reporting.*` projeksiyonu (model dok. sec. 4).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-019-grant-request-engine-and-event.md`
- `database/migrations/V1/V1-IAM-019/**`
- `src/Modules/Identity/Authorization/019/**`
- `tests/Modules/Identity/Authorization/019/**`
- `evidence/V1-IAM-019/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## Dependencies

- V1-IAM-018

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` 0 uyari / 0 hata; `dotnet test` yetkilendirme filtresi bu gorevin yeni testleriyle gecer.
- Veri degisiyorsa migration ileri/geri bos veritabaninda denenir.
- Semih icin gercek senaryo: garson kendi cekinde comp dener -> istek olusur; baska garsonun cekinde dener -> yoneticiye ulasmadan auto-deny; `reporting` "comp TL by reason by server by day" satiri uretilir.

## Handoff

- V1-IAM-020
- V1-IAM-023
