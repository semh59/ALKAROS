# V1-RMD-019 - Kitchen and operations workspace UI

- Task ID: V1-RMD-019
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Planned

## Goal

Station ticket, printer recovery ve manager health/backup görünümünü gerçek V1-RMD-015 contract'ına bağlayan role-aware
production workspace üretmek. Failure ve Unknown durumları açık, actionable ve veri-minimize edilmiş kalır.

## Owned surface

- `evidence/V1-RMD-019/**`
- PO:2026-08-28 `RMD032-F001` kararıyla kitchen operations client source/test remediation custody'si V1-RMD-034'e
  devredildi; bu historical task closed kalır.

## Dependencies

- V1-RMD-015
- V1-RMD-016

## Acceptance evidence

- Station-scoped dense ticket board/list, item transition, printer/queue alerts, Unknown/reprint recovery ve manager
  health/recent-backup views gerçek API ile çalışır. Kitchen role customer/personnel/payment detail veya catalog admin
  yüzeyi almaz; supervisor reprint reason girmeden command gönderemez.
- Default, loading, empty, busy, success, error, offline, stale, unauthorized ve conflict durumları ayrı ve bounded'dır.
  Unknown otomatik reprint olmaz; failed/unknown health yeşil veya başarılı gösterilmez.
- `pnpm test`, `pnpm typecheck`, `pnpm build` ve ilgili kitchen/operations workspace testleri exit code `0` verir;
  keyboard, 44x44 target, WCAG 2.2 AA, focus ve responsive checks geçer.
- Semih kitchen rolüyle bir item'ı ilerletirken diğerini `Preparing` bırakır; supervisor reason girmeden Unknown'u
  çözemez, reason ile çözer; failed backup sonucu görünür biçimde failed kalır.

## Handoff

- V1-RMD-020
