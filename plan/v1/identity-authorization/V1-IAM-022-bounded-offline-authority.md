# V1-IAM-022 - Bounded Offline Authority

- Task ID: V1-IAM-022
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Sınırlı çevrimdışı yetki: Host, oturum başında cihaza imzalı ve kısa ömürlü bir öz onay bütçesi verir (izin bazında tutar ile adet, sona erme damgası); çevrimdışı `grant` eylemleri bütçe elverdikçe yerelde harcanır, bütçe bitince yeniden bağlanma durumuna geçer; bağlantı dönünce her çevrimdışı yetki sunucuda yeniden doğrulanır ve inceleme için işaretlenir. Bu, karar dokümanının beşinci bölümü ile DESIGN.md birinci bölümdeki asimetrik dayanıklılık ilkesidir.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-022-bounded-offline-authority.md`
- `database/migrations/V1/V1-IAM-022/**`
- `src/Modules/Identity/Authorization/022/**`
- `tests/Modules/Identity/Authorization/022/**`
- `evidence/V1-IAM-022/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; bu görevin yeni testleriyle ilgili `dotnet test` süzgeci geçer.
- Veri değişiyorsa migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: cihaz çevrimdışıyken garson bütçe içinde tek bir void yapar ve işlem yerelde geçip kuyruğa yazılır; ikinci void bütçe dışında kaldığı için engellenir; bağlantı dönünce ilk void sunucuda yeniden doğrulanır ve yönetici incelemesine düşer; sona erme damgasından sonra tekrar oynatma reddedilir.

## Handoff

- V1-IAM-023
