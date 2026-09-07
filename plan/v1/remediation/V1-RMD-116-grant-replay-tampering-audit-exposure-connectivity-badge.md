# V1-RMD-116 - Deep audit wave 1: grant-replay tampering, kitchen audit exposure, cashier connectivity badge

- Task ID: V1-RMD-116
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla, "V1.1 kadar sıfır context ajanlar ile derin ve detaylı
denetleme yap" (12 sıfır-context ajan raporu: 5 ilk tur + 7 modül-bazlı
derin tur, `scratchpad/audit-*.md` + `scratchpad/deep-*.md`) sonrası
"Sırayla başla" onayıyla düzeltmenin ilk dalgası. Yeni denetim turunun üç bağımsız, düşük riskli, yüksek etkili bulgusunu
düzeltir: (1) yetkilendirme grant tekrar-oynatma (replay) kontrolünün
Amount/ReasonCode'u karşılaştırmaması — onaylanmış bir indirim tutarının
istemci tarafından sessizce değiştirilebilmesine izin veriyordu; (2)
mutfak modülünün sistem-geneli denetim izni uç noktalarının hiçbir izin
kontrolü olmadan herhangi bir kimliği doğrulanmış role açık olması; (3)
Cashier'ın "Çevrimiçi" rozetinin tamamen ölü/statik markup olması — LAN
kesintisinde bile hep "Çevrimiçi" göstermesi.

## Owned surface

- `plan/v1/remediation/V1-RMD-116-grant-replay-tampering-audit-exposure-connectivity-badge.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Modules/Identity/Authorization/Grants/AuthorizationGrantService.cs
    (V1-IAM-023 sahipliğinde) — MatchesReplay Amount/ReasonCode karşılaştırması.
  - src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs
    (V1-RMD-082 sahipliğinde) — iki audit uç noktasına reports.view gate'i.
  - src/Clients/Cashier/wwwroot/index.html, cashier-app.css, cashier-app.js
    (V1-RMD-083 sahipliğinde) — gerçek bağlantı rozeti.
  - tests/Modules/Identity/Authorization/Grants/AuthorizationGrantServiceTests.cs
    (V1-IAM-021 sahipliğinde), tests/Host/Experience/Billing/BillingSplitHttpTests.cs
    (V1-RMD-040 sahipliğinde), tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
    (V1-RMD-082 sahipliğinde), tests/Clients/StaticApps/cashier-app.test.js
    (V1-RMD-109 sahipliğinde), tests/Clients/Cashier/Frontend/test_cashier_frontend.py
    (V1-CUI-005 sahipliğinde) — her birine bu görevin bulgusuna karşılık gelen
    regresyon testi.

## In scope

1. **Grant-replay tampering (Critical, Billing deep-audit report C-1).**
   `AuthorizationGrantService.MatchesReplay` compared only
   `PermissionCode`/`RequesterUserId`/`SubjectType`/`SubjectId`. A manager
   approves e.g. a 5% discount grant (status moves to `Granted`, amount/
   reason frozen by the table's own transition trigger); if the client (or
   an attacker holding the same session) later resent
   `POST .../bills/{billId}/discount` with the SAME `IdempotencyKey` but a
   DIFFERENT `Value` (e.g. 95%), the bare key lookup matched anyway and
   `BillingSplitApplication.cs` applied the *new*, never-reviewed amount
   from the current request body — not the one actually approved. Fixed by
   adding `Amount`/`ReasonCode` to `MatchesReplay`; a mismatched resend now
   throws the already-existing, already-mapped `IdempotencyKeyReusedException`
   (409 `IDEMPOTENCY_KEY_REUSED`) instead of silently re-authorizing a
   different value. `BillingSplitApplication.cs` itself needed no change:
   by the time it reads `request.Value`, the replay check has already
   proven that value matches what was granted (or it's the very first,
   authoritative request).
2. **Global audit trail exposed with no permission check (High, Kitchen
   deep-audit report F-1).**
   `GET /kitchen-operations/audit/aggregate/{aggregateType}/{aggregateId}`
   and `.../audit/correlation/{correlationId}` query the system-wide
   `audit.audit_events` table (every module's void/comp/discount decisions
   included) but were gated only by `RequireReadAsync` — any authenticated
   role (waiter/cashier/supervisor/manager), same as a plain "read the
   ticket list" call. Changed both to `RequirePermissionAsync(...,
   ApplicationPermissions.ReportsView, ...)`, the same gate
   `AuthorizationDecisionEndpoints` already uses for its own audit-adjacent
   surface (supervisor/manager only per migration 043's role grants).
3. **Dead "Çevrimiçi" connectivity badge (Critical, Interface audit).**
   Cashier's `index.html` hardcoded a permanently-"online" badge with no
   `navigator.onLine` check and no `online`/`offline` listener anywhere in
   `cashier-app.js` (unlike WaiterPwa's status ribbon) — contradicting
   `DESIGN.md`'s LAN-outage protocol ("Kasiyer Kiosk salt okunura geçer").
   The badge now reflects real connectivity: `updateConnectivityBadge()`
   reads `navigator.onLine` at startup and on both `window` events, toggling
   `session-pill--online`/`session-pill--offline` (new CSS variant, red) and
   the Turkish label Çevrimiçi/Çevrimdışı.

## Out of scope

- **Cashier kiosk'un LAN kesintisinde gerçekten salt-okunura geçmesi**
  (`DESIGN.md`'nin aynı satırının ikinci yarısı) — bu, girişin nasıl
  kilitleneceğine dair ayrı bir ürün kararı gerektiriyor; `V1-RMD-115`'te
  de aynı gerekçeyle ("kasıtlı bir güvenlik/tutarlılık tercihi olabilir")
  kapsam dışı bırakılmıştı. Bu görev yalnız rozetin doğruluğunu düzeltir.
  WaiterPwa'nın IndexedDB kuyruğuna benzer bir çevrimdışı kuyruk Cashier'a
  hiç eklenmedi — Cashier'ın tek-seferlik hızlı-satış modeliyle
  `V1-RMD-051`'in kendi tasarım kararı, ayrı bir kapsam.
- Yeni denetim turunun geri kalan tüm bulguları (Orders'ın two-phase retry
  Critical'ı, Tables'ın rezervasyon yaşam döngüsü Critical'ları, Catalog'un
  `current_price`/modifier-group Critical'ları, boundary'nin Host-katmanı
  ham SQL'i, database'in Purchasing atomiklik sorunu, vb.) — ayrı, daha
  büyük/riskli dalgalara bırakıldı; bu görev yalnız en izole, en düşük
  riskli üç bulguyu kapsar.

## Dependencies

- V1-RMD-115

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --build --rm test`:
  tüm proje testleri yeşil (yeni testler dahil:
  `AuthorizationGrantServiceTests.ReplayingAnIdempotencyKeyWithADifferentAmountThrows`,
  `...WithADifferentReasonCodeThrows`,
  `BillingSplitHttpTests.ResendingAnApprovedIdempotencyKeyWithATamperedAmountIsRejectedNotApplied`,
  `KitchenOperationsHttpTests.HealthBackupPrinterRouteAndAuditSurfacesAreMinimizedAndFailClosed`
  genişletilmiş hali).
- `npm test` (`tests/Clients/StaticApps`): 2 dosya, 8 test, hepsi geçti
  (yeni `reflects the terminal's real connectivity instead of a static badge`
  dahil).
- `pytest tests/Clients/Cashier/Frontend/test_cashier_frontend.py`: 4/4 geçti.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı, dokunulmayan dosyalarda (aynı 13 satır,
  `V1-RMD-115`'in kaydettiğiyle birebir aynı).

## Handoff

- V1-GOV-109
