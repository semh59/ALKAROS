# V1-IAM-023 - Behavioural Tightening

- Task ID: V1-IAM-023
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Davranışsal sıkılaştırma: kişi başına yuvarlanan oran anlık görüntüsü (void yüzdesi, vardiya başına ikram tutarı, indirim adedi) tutulur; oranı otuz günlük tabanının üç katına ya da üzerine çıkan kullanıcı, rolü normalde kendiliğinden onaylasa bile bir yönetici temizleyene kadar yetki isteğine zorlanır.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-023-behavioural-tightening.md`
- `database/migrations/V1/V1-IAM-023/**`
- `src/Modules/Identity/Authorization/023/**`
- `tests/Modules/Identity/Authorization/023/**`
- `evidence/V1-IAM-023/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı ile sıfır hata; bu görevin yeni testleriyle ilgili `dotnet test` süzgeci geçer.
- Veri değişiyorsa migration ileri ile geri yönde boş veritabanında denenir.
- Semih için gerçek senaryo: test kullanıcısının void oranı üç katına çıkarılır ve normalde kendiliğinden geçen void artık yönetici isteği üretir; yönetici temizle der ve akış yeniden kendiliğinden onaya döner; her geçiş bir denetim satırı bırakır.

## Handoff

- V1-IAM-024
