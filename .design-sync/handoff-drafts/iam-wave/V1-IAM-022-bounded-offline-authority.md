# V1-IAM-022 - Bounded Offline Authority

- Task ID: V1-IAM-022
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Sinirli cevrimdisi yetki: Host oturum basinda cihaza imzali, kisa TTL'li oz-onay butcesi verir (permission -> {amount,count}, `exp`); cevrimdisi `grant` eylemleri butce oldukca yerel harcanir, bitince "yeniden baglan" durumu; baglaninca her cevrimdisi yetki sunucuda yeniden dogrulanir ve `offline_pending_review` isaretlenir (model dok. sec. 5; DESIGN.md sec. 1 asimetrik dayaniklilik).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-022-bounded-offline-authority.md`
- `database/migrations/V1/V1-IAM-022/**`
- `src/Modules/Identity/Authorization/022/**`
- `tests/Modules/Identity/Authorization/022/**`
- `evidence/V1-IAM-022/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` 0 uyari / 0 hata; `dotnet test` yetkilendirme filtresi bu gorevin yeni testleriyle gecer.
- Veri degisiyorsa migration ileri/geri bos veritabaninda denenir.
- Semih icin gercek senaryo: cihaz cevrimdisi; garson butce icinde 1 void yapar -> yerel gecer, kuyruga yazilir; ikinci void butce disi -> bloke; baglaninca ilk void sunucuda yeniden dogrulanir ve yonetici incelemesine dusrer; butce `exp` sonrasi replay reddedilir.

## Handoff

- V1-IAM-023
