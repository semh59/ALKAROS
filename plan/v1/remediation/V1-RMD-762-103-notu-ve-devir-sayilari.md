# V1-RMD-762 - V1-RMD-103 notu ve 739 devir sayıları güncel hâle getirilir

- Task ID: V1-RMD-762
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-760'ta kapsam dışı kalan iki tutarsızlık:

1. V1-RMD-103 notu kalem bölme reddini yalnız payable farkı olarak anlatıyor; V1-RMD-754'ten beri vergi farkı da reddedilir. Kanıt klasörü olmayan Done görevin planı kanıt gelmeden değiştirilemediği için `evidence/V1-RMD-103/` altına bu değişikliğin gerçek denetim çıktıları konur.
2. `evidence/V1-RMD-739/DEVIR.md` kalan iş sayısı, sonradan kapanan bulguları (F0388, F0908, F0068, F0077) düşmüyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-762-103-notu-ve-devir-sayilari.md`
- `evidence/V1-RMD-762/**`
- Sınırlı ek: plan/v1/remediation/V1-RMD-103-older-audit-remaining-high-medium-findings.md
- Sınırlı ek: evidence/V1-RMD-103/**
- Sınırlı ek: evidence/V1-RMD-739/DEVIR.md

## In scope

- 103 notuna "V1-RMD-754 sonrası vergi farkı da reddedilir" eklenir; görev metninin tarihsel kapsamı değişmez.
- `evidence/V1-RMD-103/` altına plan-audit ve consistency-audit gerçek çıktıları.
- DEVIR.md'ye kapanan bulguların kayıt satırı (sayı yeniden hesaplanmaz).

## Out of scope

- Üretim kodu, testler, 739 sınıflandırma kayıtları ve `register.md`.

## Dependencies

- V1-RMD-754
- V1-RMD-760

## Acceptance evidence

1. Plan audit ve consistency audit 0 hata, 0 uyarı; çıktıları kaydedilir.
2. Kapsam aracı InProgress baseline'a karşı exit 0 verir.

## Handoff

- None
