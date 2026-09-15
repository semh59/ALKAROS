# V1-RMD-210 - `IsValidWaiterAsync` artık kanonik yetkilendirme sorgusunu kullanıyor

- Task ID: V1-RMD-210
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız `/code-review` denetiminin (2026-09-15) bulduğu bir bulgu:
`SuggestedWaiterResolver.IsValidWaiterAsync` (V1-RMD-207), kullanıcının
aktif olup olmadığını ve `orders.send` taşıyıp taşımadığını kendi elle
yazdığı ham SQL ile kontrol ediyordu — hâlbuki `IRoleRepository` zaten
bunun için `GetUserStateAsync` (aktiflik) ve
`GetPermissionCodesForUserAsync` (izin listesi) sağlıyor, kod tabanının
geri kalanının (`RequireCashierPermissionAsync`, `AuthorizationService`)
kullandığı aynı kaynak. Bu, garson atama gibi gerçek bir yetkilendirme
kararının kanonik kuraldan sessizce sapabileceği bir kopya kod riskiydi
— rol/izin kapsamı, süre dolumu gibi kurallar ileride değişirse
`IsValidWaiterAsync` unutulup eski davranışta kalabilirdi.

## Owned surface

Sınırlı ek (yollar geri-tik olmadan, V1-RMD-111 emsali):

- src/Host/Experience/Orders/SuggestedWaiterResolver.cs (paylaşılan
  dosya) — yeni `IRoleRepository` bağımlılığı; `IsValidWaiterAsync`
  artık `GetUserStateAsync` + `GetPermissionCodesForUserAsync`'i
  kullanıyor, kendi ham SQL'i silindi.
  `ResolveMostSuitableWaiterAsync`'in aday sorgusu değişmedi (o, tek
  sorguda yük/rotasyon/bölge birlikte sıralandığı için ayrı bir SQL
  sorgusu olarak kalmaya devam ediyor — `IRoleRepository`'nin API'si
  "kim uygun" sıralaması yapmıyor, yalnız tekil kullanıcı sorgularıyor).
- tests/Host/Experience/Orders/TableDraft/** (ilgili görev
  sahipliğinde) — davranış aynı kaldığı için mevcut testler değişmedi.

## In scope

1. `IsValidWaiterAsync(userId, ct)`: `roles.GetUserStateAsync(userId,
   ct)` ile var/aktif kontrolü, ardından
   `roles.GetPermissionCodesForUserAsync(userId, ct)`'in
   `orders.send` içerip içermediği — ikisi de kanonik kaynak.

## Out of scope

- `ResolveMostSuitableWaiterAsync`'in kendi adaylık/sıralama sorgusu —
  `IRoleRepository`'nin tekil-kullanıcı API'siyle ifade edilemeyecek bir
  toplu sıralama, bu görevin kapsamında değil.

## Dependencies

- V1-RMD-207

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Host/Experience/Orders/TableDraft` →
  tüm testler yeşil (V1-RMD-207'nin doğrulama senaryoları dahil,
  davranış değişmedi); revert-and-confirm ile en az bir test gerçekten
  kırılıp doğrulanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: V1-RMD-207'nin elle senaryosu
  aynen geçerli (rastgele GUID'e atama 400 döner) — davranış aynı,
  yalnız artık kanonik kaynağı kullanıyor.

## Handoff

- None
