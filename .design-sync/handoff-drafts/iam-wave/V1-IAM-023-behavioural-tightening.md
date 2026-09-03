# V1-IAM-023 - Behavioural Tightening

- Task ID: V1-IAM-023
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Davranissal sikilastirma: yuvarlanan kisi-basi oran anlik goruntusu (void %, comp TL/vardiya, indirim sayisi); orani 30-gunluk tabaninin >= 3 katina cikan kullanici, rolu normalde otomatik onaylasa bile bir yonetici temizleyene kadar `requires_grant`'e alinir.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-023-behavioural-tightening.md`
- `database/migrations/V1/V1-IAM-023/**`
- `src/Modules/Identity/Authorization/023/**`
- `tests/Modules/Identity/Authorization/023/**`
- `evidence/V1-IAM-023/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` 0 uyari / 0 hata; `dotnet test` yetkilendirme filtresi bu gorevin yeni testleriyle gecer.
- Veri degisiyorsa migration ileri/geri bos veritabaninda denenir.
- Semih icin gercek senaryo: test kullanicisinin void orani 3x firlatilir -> normalde auto olan void artik yonetici istegi uretir; yonetici "temizle" der -> tekrar auto; her gecis denetim satiri.

## Handoff

- V1-IAM-024
