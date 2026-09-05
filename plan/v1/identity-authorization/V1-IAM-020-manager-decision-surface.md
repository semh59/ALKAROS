# V1-IAM-020 - Manager Decision Surface

- Task ID: V1-IAM-020
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

Yönetici / şef garson karar yüzeyi (karar dokümanı §4 adım 3): bekleyen yetki
isteği tam bağlam bloğuyla (izin, tutar, `reason_code`, isteyen personel)
yöneticiye düşer, ilk yanıt kazanır ve yetki kaydı `policy_path=manual` ile
`approver_user_id` taşır. Aynı yüzey etkin süreli devirleri (V1-IAM-021) geri
alır ve açık davranışsal sıkılaştırmaları (V1-IAM-023) temizler. Host
`/api/v1/management/authorization` uç grubu (`AuthorizationDecisionStore` +
`AuthorizationDecisionEndpoints`, manager/supervisor cihaz oturumu artı
`reports.view`); PosTerminal `authorization-decisions` çalışma alanı (`/authorization`
rotası, `reports.view` yeteneği); WaiterPwa `WaiterManagerDecisionEngine` (şef
garson telefonda: salt-okunur liste, reconnect'te sunucu anlık görüntüsüne
yakınsama, çözülen isteği bir sonraki anlık görüntüye kadar gizleme).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-020-manager-decision-surface.md`
- `src/Host/Experience/Authorization/**`
- `tests/Host/Experience/Authorization/**`
- `src/Clients/PosTerminal/src/features/authorization-decisions/**`
- `src/Clients/WaiterPwa/ManagerDecisions/**`
- `tests/Clients/WaiterPwa/ManagerDecisions/**`
- Paylaşılan dosyalarda sınırlı ek: ALKAROS.slnx sahipliği foundation'da kalır, bu görevde yalnızca iki yeni test proje girişi eklendi (V1-FND-002 deseni). src/Host/DualScreen/DualScreenApplication.cs sahipliği V1-RMD-098'de kalır, bu görevde yalnızca AddAuthorizationDecisionExperience ile MapAuthorizationDecisionApi satırları eklendi. src/Clients/PosTerminal/src/routes/workspace.tsx ile src/Clients/PosTerminal/src/strings.ts sahipliği V1-RMD-097'de kalır, bu görevde yalnızca /authorization rotası, navigasyon girişi ve etiket eklendi (V1-RMD-089/9. dalga deseni, PO:2026-09-04).
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-019
- V1-IAM-021
- V1-IAM-023

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata.
- `dotnet test`: `ALKAROS.Host.Experience.Authorization.Tests` 5/5 — oturumsuz
  istek 401, `reports.view` yoksa 403, iki yönetici aynı bekleyen isteği
  bağlam bloğuyla görür, ilki `approve` deyince kayıt
  `status=granted`/`policy_path=manual`/`approver_user_id=onaylayan` olur ve
  ikinci `approve` 409 `ALREADY_RESOLVED` alır, çözülen istek listeden düşer;
  `deny` yolu `status=denied`/`manual`; devir listelenir, `revoke` 204,
  tekrar `revoke` 404; sıkılaştırma listelenir, `clear` 204, tekrar `clear`
  409 `ALREADY_CLEARED`; rota kayıt testi yedi ucu yayımlar.
  `ALKAROS.WaiterPwa.ManagerDecisions.Tests` 6/6 — bağlantı kopunca stale,
  reconnect sunucu anlık görüntüsüne yakınsar, yerel çözülen istek anlık
  görüntü düşürene kadar gizli, bilinmeyen id yok sayılır, doğrudan mutasyon
  reddi. `ALKAROS.Identity.Authorization.Tests` 178/178 (Debug; Release DLL
  yolunda geçici WDAC engeli, kod değişmedi).
- Frontend: `tsc --noEmit` temiz; `vitest run` 106/106 (yeni
  `AuthorizationDecisionsWorkspace.test.tsx` 4 — üç grup Türkçe izin/gerekçe
  etiketleriyle çizilir ve ham kod sızmaz, onayla/reddet/geri al/kaldır
  callback'lere bağlanır, boş kopya, yönetici olmayan görüntüleyici engellenir).
- Migration yok; şema değişmedi.
- Semih için gerçek senaryo: garson kendi çekinde ₺120 ikram ister, politika
  tırmanmaya düşürür ve istek `pending` olur. İki yönetici PosTerminal
  `/authorization` ekranında ya da şef garson telefonunda aynı isteği görür;
  biri Onayla der, kayıt `granted` / `policy_path=manual` / onaylayan kişiyle
  kapanır ve diğer yönetici tekrar denerse 409 alır, istek her iki ekrandan
  da düşer.

## Handoff

- V1-IAM-024
