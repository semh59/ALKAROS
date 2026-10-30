# V1-RMD-760 - Son 3 günün plan ve kanıt tutarsızlıklarını düzelt

- Task ID: V1-RMD-760
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: documentation
- Surface state: Existing

## Goal

Son üç günün (V1-RMD-740..757) denetiminde bulunan, üretim kodunu etkilemeyen plan ve kanıt tutarsızlıkları:

1. V1-RMD-026 notu iki test yolunun HEAD'de bulunmadığını söylüyor; yollar `FloorPlan*.cs` desenidir ve dosyalar vardır.
2. V1-RMD-103 notu kalem bölme reddini yalnız payable farkı olarak anlatıyor; V1-RMD-754'ten beri vergi farkı da reddedilir.
3. V1-RMD-755 `RESULT.md` kendini hâlâ "kapanış bekliyor, InProgress" diye tanımlıyor; plan Done.
4. V1-RMD-753 Blocked kalmış; blocker'ı V1-RMD-755'in kanıtlanmasına bağlıydı ve 755 Done oldu. Kabul kanıtları 755 klasöründe.

## Owned surface

- `plan/v1/remediation/V1-RMD-760-plan-ve-kanit-tutarsizliklari.md`
- `evidence/V1-RMD-760/**`
- Sınırlı ek: plan/v1/remediation/V1-RMD-026-floor-plan-persistence-and-api.md
- Sınırlı ek: plan/v1/remediation/V1-RMD-103-older-audit-remaining-high-medium-findings.md
- Sınırlı ek: plan/v1/remediation/V1-RMD-753-split-engine-host-billing-test-gaps.md
- Sınırlı ek: evidence/V1-RMD-755/RESULT.md
- Sınırlı ek: evidence/V1-RMD-753/CLOSURE.md

## In scope

- 026 yanlış notunu düzelt; 103 notuna vergi farkı reddini ekle.
- 755 `RESULT.md` başına kapanış durumunu yaz (Done, kanıt koşulları); eski ara metni silme.
- 753: Status Done; `evidence/V1-RMD-753/CLOSURE.md` kabul maddelerini 755 klasöründeki gerçek dosyalarla eşler (TRX adları, `guard-mutation-results.json`). Tarihsel kabul metni değişmez.

## Out of scope

- Üretim kodu ve test dosyaları.
- Takipsiz `evidence/V1-RMD-755/isolated/` silinmesi (Semih ayrıca onaylar).
- V1-RMD-739 DEVIR.md sayıları (739 sahibinin yüzeyi).

## Dependencies

- V1-RMD-755
- V1-RMD-754

## Acceptance evidence

1. Plan audit ve consistency audit 0 hata, 0 uyarı.
2. 753 `CLOSURE.md` her kabul maddesi için var olan bir dosyaya ve geçen bir teste işaret eder; işaret edilen yollar diskte vardır.
3. Kapsam aracı InProgress baseline'a karşı exit 0 verir.

## Handoff

- None
