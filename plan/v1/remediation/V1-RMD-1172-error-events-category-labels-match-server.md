# V1-RMD-1172 - Hata ekranı: sunucunun ürettiği her olay kategorisinin Türkçe adı vardır ve bir daha ayrışmaz

- Task ID: V1-RMD-1172
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1170 sonrası taramada sunucu kaynaklarında (src/Host/Observability, src/Modules/Observability) üretilip `categoryLabel` sözlüğünde olmayan dokuz kategori bulundu; hata ekranı bunları "Bilinmiyor" gösteriyor: `resource.disk_high`, `resource.memory_high`, `outbox.dead_letter`, `monitoring.retention_failed`, `monitoring.outbox_watch_failed`, `monitoring.detector_degraded`, `perf.budget_exceeded`, `perf.slowdown`, `perf.regression_after_release`. Dokuzuna Türkçe ad verilir. `system.unreachable` yalnız kritik kesinti kanalına (Telegram, hata kaydı değil) gider ve `monitoring.exception_reported` bir yinelenme anahtarıdır; ikisi de hata ekranına düşmez, bu yüzden adlandırılmaz (ölü sözlük girdisi olmasın). Sözlük ile sunucu kategorileri bir daha ayrışmasın diye vitest, sunucu kaynak dosyalarını tarayıp bulunan her kategorinin sözlükte olduğunu doğrular; bu iki istisna gerekçesiyle testte açıkça yazılıdır.

## Owned surface

- `evidence/V1-RMD-1172/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/management-error-events/models.ts
  - src/Clients/PosTerminal/src/features/management-error-events/ErrorEventsSection.test.tsx
  - src/Clients/PosTerminal/src/features/management-error-events/serverCategoryLabels.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (dokuz kategori ve sunucu-kaynak tarama testi), düzeltme, mutasyon (`cp` + `cmp`; sözlükten bir girdi silinince tarama testi kırmızı)
2. İki bağımsız qa incelemesi
3. `pnpm run typecheck`, ilgili vitest ve `node tools/client-build-gate/clientBuildGate.js --base <başlangıç commit'i>` exit code 0

## Out of scope

- Sunucu kodunda kategori adı değişikliği; olay ayrıntı metni; katman, önem ve durum sözlükleri (sunucu enum'larını zaten kapsıyor)

## Dependencies

- V1-RMD-1170

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; RESULT.md tarama testinin hangi sunucu dizinlerini taradığını ve iki istisnanın gerekçesini yazar
2. Typecheck, vitest ve istemci derleme kapısı exit code 0; plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
