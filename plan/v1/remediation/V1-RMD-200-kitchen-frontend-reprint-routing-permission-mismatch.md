# V1-RMD-200 - Mutfak ekranı: reprint ve rota yönetimi butonları yanlış izinle kapılı

- Task ID: V1-RMD-200
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteğiyle çalıştırılan 6 boyutlu bağımsız ajan denetiminde
(2026-09-14) İKİ AYRI ajan (frontend + rol/yetki) birbirinden bağımsız
olarak AYNI gerçek hatayı buldu: `src/Clients/PosTerminal/src/routes/
workspace.tsx`, reprint onay/red panelini VE kategori-yazıcı rota
formunu `canOperate` (`orders.send`) ile kapılıyordu. Ama backend bu iki
aksiyonu SIRASIYLA `kitchen.reprint` (`KitchenOperationsEndpoints.
ReprintPermission`) ve `kitchen.routing.manage`
(`RoutingMutationPermission`) ile koruyor — `orders.send`'in üst kümesi
DEĞİL, tamamen ayrı izinler. `orders.send`'i taşıyan ama `kitchen.
reprint`/`kitchen.routing.manage`'i TAŞIMAYAN roller (waiter, cashier —
ikisi de reprint için; waiter, cashier, supervisor — üçü de routing
için) bu butonları aktif görüyor, tıklayınca backend'den 403 alıyor.
Yetki-atlama değil (backend doğru reddediyor) ama kullanıcıyı yanıltan,
güveni sarsan bir arayüz-backend uyuşmazlığı.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/models.ts
  (Sınırlı ek — V1-KDS-001 sahipliğinde kalan dosya) — `KitchenWorkspaceProps`'a
  yeni `canManageRouting: boolean` alanı.
- src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
  (Sınırlı ek) — `PrinterPanel`'in kendi `canOperate` prop'u
  `canManageRouting` olarak yeniden adlandırıldı (artık `canOperate`'ten
  değil, yeni prop'tan besleniyor).
- src/Clients/PosTerminal/src/routes/workspace.tsx (Sınırlı ek,
  paylaşılan — V1-KDS-001 sahipliğinde kalan dosya) —
  `canManageReprints={capabilitySet.has("kitchen.reprint")}` (önceden
  `canOperate`), yeni `canManageRouting={capabilitySet.has("kitchen.
  routing.manage")}`, `onCreateCategoryRoute` artık `canManageRouting`
  ile kapılı (önceden `canOperate`).

## In scope

1. Reprint onay/red paneli (`UnknownPanel`) artık `kitchen.reprint`
   izniyle kapılı.
2. Kategori-yazıcı rota formu (`PrinterPanel`'in `onCreateCategoryRoute`
   kısmı) artık `kitchen.routing.manage` izniyle kapılı.
3. `canOperate` (`orders.send`) bu ikisinden artık TAMAMEN ayrıldı — her
   izin kendi backend gereksinimiyle birebir eşleşiyor.

## Out of scope

- `PUT /printers/{id}` (yazıcı aktif/pasif) — frontend'de hiç
  çağrılmıyor (kod tabanında bu ucu tüketen bir yer yok), bu görevin
  kapsamı dışında.
- Bu oturumun 6 boyutlu denetiminde bulunan diğer bulgular (bkz.
  V1-RMD-199/201/202).

## Dependencies

- None

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil; yeni
  testler — `kitchen.reprint` yokken (yalnız `orders.send` varken)
  reprint panelinin "Süpervizör gerekli" gösterdiği/butonların
  görünmediği; `kitchen.routing.manage` yokken rota formunun
  görünmediği; ikisi de varken çalıştığı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- Semih'in elle deneyebileceği senaryo: bir `cashier` oturumuyla mutfak
  ekranını aç, reprint onay/red butonlarının VE rota formunun artık
  görünmediğini (önceden görünüp 403 ile patlıyordu) doğrula;
  `supervisor` oturumuyla reprint butonlarının göründüğünü ama rota
  formunun (supervisor `kitchen.routing.manage` taşımıyor) hâlâ
  görünmediğini doğrula.

## Handoff

- None
