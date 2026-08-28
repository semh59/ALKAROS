# V1-RMD-010 validation — 2026-08-25

## Verdict

Repository-içi UI remediasyonu uygulanıp otomatik kontrollerden geçti. Unique test CA kullanıcı tarafından görünür
Windows güven penceresinde onaylandı ve gerçek Production Kestrel HTTPS health endpoint'i normal trust altında `200`
verdi. Görev yine fail-closed `Blocked` kalır; çünkü kullanıcı browser ailesi belirtmediği için zorunlu target-URL
selector ile yapılan düzeltilmiş `getForUrl('https://localhost:49440/')` çağrısı `No browser is available` döndürdü.
Bu nedenle iki bağımsız browser storage alanıyla login → pairing → mutation → submit → reconnect → restart →
stale → revoke ve gerçek 200%/400% zoom zinciri tamamlanamadı. Bypass veya fixture backend kanıtı kullanılmadı.

## Reproducible checks

- `pnpm install --frozen-lockfile`: exit `0`.
- `pnpm test`: exit `0`, 1 file / 9 tests. Axe-core 4.10.3 ile iki DOM state'inde critical/serious ihlal `0`;
  jsdom ölçemediği için color-contrast kuralı bu otomatik semantic turda kapalıdır, browser görsel turundan ayrı tutulur.
- `pnpm typecheck`: exit `0`.
- `pnpm build`: exit `0`; CSS `22.70 kB`, JS `271.01 kB`.
- `python -B tools/plan-audit/plan_audit_tool.py validate`: exit `0`; 404 Markdown, 382 task, 0 error/warning.
- Scoped `git diff --check`: exit `0`.
- Stub scan yalnız iki gerçek HTML `placeholder` attribute'u buldu; TODO/FIXME/mock-success/empty catch bulunmadı.

## Browser evidence

- `cashier-viewport-matrix.json`: 9 hedef viewport + `1250px` ve `900px` breakpoint'lerinin ±1px sınırları. Tüm
  ölçümlerde görünen actionable target minimum `44px`, kritik CTA/toplam/bağlantı görünür ve undersized listesi boş.
- `screenshots/cashier-*.png`: 15 viewport ekran görüntüsü. Fixture yalnız deterministik UI render kanıtıdır.
- `focus-modal-report.json`: modal adı, `aria-modal`, Shift+Tab trap, Escape ve trigger focus restoration doğrulandı.
- `screenshots/display-active-1440x900.png`, `display-paying-1440x900.png`,
  `display-completed-thank-you-1440x900.png`, `display-completed-safe-idle-1440x900.png`,
  `display-unavailable-1440x900.png`: contract state render kanıtı. Fixture backend kanıtı değildir.
- Completed ilk render ve 7.2 saniye sonraki safe-idle render'ında parasal metin/total card yoktur. Unavailable ilk
  render'da parasal veri yoktur ve çalışan retry CTA görünür.
- `screenshots/real-host-pairing-390x844.png` ve `real-host-pairing-dom.txt`: `localhost` / `127.0.0.1` host-scope
  ayrımıyla gerçek Host'tan alınan pairing code. Transport HTTP'dir; HTTPS kanıtı değildir.

## Remaining blockers

1. Browser-client target-URL selector bu desktop runtime'da browser bağlayamıyor: `No browser is available`.
2. Disposable PostgreSQL + gerçek kimlik bilgisiyle cashier login/order mutation/submit/reconnect/restart/revoke zinciri
   bu görev oturumunda tamamlanamadı.
3. Browser yüzeyi programatik 200%/400% zoom seviyesini raporlamadı; Ctrl+zoom girişimi layout ölçeğini değiştirmedi,
   dolayısıyla zoom acceptance kanıtlanmış sayılmadı.
4. Gerçek SignalR reconnect/restart zinciri tamamlanmadı. Fixture SignalR sağlamaz ve bu sınır açıkça ayrıştırılmıştır.

## HTTPS continuation and fail-closed recovery — 2026-08-25

- Exact SDK `10.0.302` ile gerçek Host Release publish edildi; disposable PostgreSQL 18 digest image üzerinde 001→038
  migration ve test kullanıcısı/catalog seed aşamasına ulaşıldı. İlk readiness denemesinde harness'ın hatalı ek
  `dual-screen` CLI argümanı tespit edilip yalnız task-local harness'ta düzeltildi; production Host değiştirilmedi.
- Sonraki unique CA importları PowerShell X509Store ve `certutil -user -f` yollarında görünür Windows
  `Güvenlik Uyarısı` açtı. User CA importunu onaylamış olsa da computer-use sözleşmesi Windows security/permission
  penceresinde otomatik inputu kesin olarak yasaklar. Diyalog kullanıcı tarafından onaylanmadığı için setup kesildi.
- Detached gerçek Git worktree kullanıldı; sahte `.git` linki veya provenance override kullanılmadı.
- `ignoreHTTPSErrors`, SPKI, browser interstitial veya güvenlik bypass uygulanmadı; fixture backend kanıtı yapılmadı.
- Üç interrupted run exact `Recover` ile temizlendi. Generated Root/My prefix sayısı, önceki exact thumbprint sayısı,
  rmd010 container/temp/worktree sayısı kapanışta sıfırdır (`cert-store-post.txt`).
- `localhost` ve `127.0.0.1` origin ayrımı gerçek HTTP Host üzerinde cookie/storage izolasyonunu ve gerçek pairing-code
  üretimini kanıtladı; bu sonuç TLS acceptance yerine kullanılmadı.

## Final reproducible checks

- Bundled Node `v24.19.0`; pnpm `11.19.0`.
- `pnpm install --frozen-lockfile`: exit `0`.
- `pnpm test`: exit `0`, 1 file / 9 tests.
- `pnpm typecheck`: exit `0`.
- `pnpm build`: exit `0`; CSS `22.70 kB`, JS `271.01 kB`.
- `python -B tools/plan-audit/plan_audit_tool.py validate`: exit `0`; 404 Markdown, 382 task, 0 error/warning.
- Scoped `git diff --check`: exit `0`.

## Visible trust unlock attempt — 2026-08-25

- Unique subject `CN=ALKAROS V1-RMD-010 Local E2E Root bcf7779caf5f42f897e8072fac9c0106` için yalnız public
  root `.cer` görünür `certutil` sürecinde kullanıcı tarafından onaylandı; Root thumbprint
  `628707E88DF6BF82FD8D8AD17ADC327EA3D63265`, leaf thumbprint
  `2F3070ECD9AB186E1BC0E40261F38EB65E073691` idi. Private key/parola evidence'a yazılmadı.
- Detached gerçek worktree commit'i `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`; Production Kestrel
  `https://localhost:49440/health/ready` normal trust ile `200` verdi. PostgreSQL 18 digest image 001→038 migration ve
  disposable login/catalog seed aşamalarını geçti.
- İlk doğrudan family denemeleri acceptance kararı olarak geri çekildi. Kullanıcı browser ailesi belirtmediği için
  düzeltilmiş zorunlu çağrı `agent.browsers.getForUrl('https://localhost:49440/')` yapıldı ve `No browser is available`
  döndü. Skill sözleşmesi nedeniyle standalone Playwright/CUA browser E2E fallback yapılmadı.
- Host PID `19096`, PG container, detached worktree ve exact temp directory kaldırıldı. Runtime/PFX sıfır byte buffer
  ile overwrite edilip silindi. Root store exact registry key'i ve My store exact thumbprint kayıtları kaldırıldı;
  final Root/My/container/temp/worktree/process sayıları `0`.

## Browser selector correction — 2026-08-25

- Bootstrap troubleshooting tekrar okundu; mevcut browser runtime yeniden initialize/reset edilmedi.
- Herhangi bir cert/Host/PG mutasyonundan önce target URL üzerinden `getForUrl` çağrıldı.
- Sonuç: `No browser is available`; browser documentation/tab binding üretilemedi.
- Bu denemede sistem mutasyonu yapılmadı ve önceki cleanup kapanış sayımları değişmedi.
- Unlock: Codex desktop Browser runtime etkin olmalı veya Settings → Computer use bölümünden ChatGPT browser extension
  kurulup bağlı olmalı; ardından aynı `getForUrl` çağrısı tab/DOM yeteneği sağlamalıdır.
