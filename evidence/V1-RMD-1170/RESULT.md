# V1-RMD-1170 sonuc
Yapildi: models.ts categoryLabel'a "backup.offsite_unavailable": "Makine dışı kopya kullanılamıyor" ve "backup.wal_archive_oversize": "Yedek kayıt arşivi sınırı aştı"; ErrorEventsSection.test.tsx kategori listesine iki kategori.
Kanit: red-vitest.log (2 kirmizi), green-vitest.log 40/40, typecheck.log exit 0, client-build-gate.log exit 0 (lint, tip, vite build), mutations.txt (iki etiket silinince test kirmizi), plan/consistency audit 0/0.
Acik risk: 5 baska sunucu kategorisi (resource.disk_high, resource.memory_high, system.unreachable, outbox.dead_letter, monitoring.retention_failed) hata ekraninda hala "Bilinmiyor"; bu gorev disi, plan gerekir.
