# V1-RMD-679 - Yetki karar ekranında kişi adları

- Task ID: V1-RMD-679
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulgusu B006'nın ad kısmı kapanır. Yetki karar ekranı, bekleyen isteği kimin istediğini, devrin kime verildiğini ve sıkılaştırmanın kimde olduğunu kişinin görünen adıyla gösterir; kimlik parçası (ilk sekiz hane) hiçbir yerde görünmez. Sunucu, üç listeye personelin görünen adını ekler (devirde devredenin adı da gelir); ad bulunamazsa ekran "Bilinmeyen personel" yazar.

## Owned surface

- `plan/v1/remediation/V1-RMD-679-decision-screen-names.md`
- `evidence/V1-RMD-679/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Authorization/AuthorizationDecisionContracts.cs - yalnız ad alanları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs - yalnız adların eklenmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/authorization-decisions/models.ts - yalnız ad alanları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.tsx - yalnız ad gösterimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.test.tsx - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-678-pending-rounds-belong-to-their-waiter.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Üç liste uçlarında ad alanları (`requesterName`, `granteeName`, `delegatorName`, `userName`); ekranda gösterim.

## Out of scope

- Adların denetim kaydına yazılması; başka ekranlardaki kimlik parçaları (ayrı denetimlerde taranır). Tarayıcıda elle deneme (doğrulanamadı).

## Dependencies

- V1-RMD-678

## Acceptance evidence

1. Sunucu testi gerçek PostgreSQL'de, ekran testi vitest; mutasyon kanıtı.
2. `evidence/V1-RMD-679/` altında çıktılar; Authorization Host ve vitest yeşil.

## Handoff

- V1-RMD-680
