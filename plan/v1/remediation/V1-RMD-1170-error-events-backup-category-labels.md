# V1-RMD-1170 - Hata ekranı iki yeni yedek alarmının Türkçe adını gösterir

- Task ID: V1-RMD-1170
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1168 iki yeni olay kategorisi ekledi: `backup.offsite_unavailable` ve `backup.wal_archive_oversize`. Yönetim hata ekranı (`categoryLabel`) bilinmeyen kategoriyi "Bilinmiyor" diye gösterir; yeni alarmlar bu yüzden adsız görünürdü. Bu görev iki kategoriye Türkçe ad verir: "Makine dışı kopya kullanılamıyor" ve "Yedek kayıt arşivi sınırı aştı" (docs/operations/yedekten-geri-donus.md bu adları kullanır).

## Owned surface

- `evidence/V1-RMD-1170/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/management-error-events/models.ts
  - src/Clients/PosTerminal/src/features/management-error-events/ErrorEventsSection.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (iki kategori için "Türkçe adı var" testi), düzeltme, mutasyon (`cp` + `cmp`; her etiket silinince test kırmızı)
2. İki bağımsız qa incelemesi
3. `pnpm run typecheck`, ilgili vitest ve `node tools/client-build-gate/clientBuildGate.js --base <başlangıç commit'i>` exit code 0

## Out of scope

- Olay ayrıntı metninin gösterimi, diğer kategoriler, sunucu tarafı

## Dependencies

- V1-RMD-1168

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları
2. Typecheck, vitest ve istemci derleme kapısı exit code 0; plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
