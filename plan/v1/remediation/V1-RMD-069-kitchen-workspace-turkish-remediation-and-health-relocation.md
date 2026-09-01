# V1-RMD-069 - Kitchen workspace Turkish remediation and health relocation

- Task ID: V1-RMD-069
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

PosTerminal mutfak çalışma alanında kullanıcıya sızan `Unknown` kod tipi metinlerini Türkçeye çevirmek, sistem sağlık ve yedek durumlarını çeviri haritalarıyla göstermek, sağlık panelini her bilet görünümünde duran yan raydan çıkarıp yalnızca yetkili kullanıcıya açık ayrı bir bölüme almak ve biletlerde oluşturulmadan bu yana geçen süreyi eşik rengiyle göstermek.

## Owned surface

- `plan/v1/remediation/V1-RMD-069-kitchen-workspace-turkish-remediation-and-health-relocation.md`
- PO:2026-08-31 kararıyla src/Clients/PosTerminal/src/features/kitchen-operations/** yüzeyi V1-RMD-075'e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-069/**`

## In scope

- `Unknown baskı`, `Unknown kaydı değişti`, `Unknown teslimat` ve `Unknown teslimatlar` metinlerinin Türkçe karşılıklarıyla değiştirilmesi.
- `databaseStatus`, `diskStatus`, `lastBackupStatus` ve yedek `status` değerleri için Türkçe etiket haritaları; sağlık noktası `aria-label` değerinin de çevrilmesi.
- Sağlık panelinin bilet raylarından çıkarılıp üst barda küçük durum göstergesi bırakılması; ayrıntılı panelin yalnızca reprint yönetim yetkisi olan kullanıcıya görünmesi.
- Biletlerde `createdAt` değerinden türetilen geçen süre rozeti ve yeşil, sarı, kırmızı eşik renklendirmesi; eşikler istasyon sabiti olarak tanımlanır.
- Yeni davranışların Vitest testleri.

## Out of scope

- Mutfak API sözleşmesini veya `KitchenTicket` domain modelini değiştirmek.
- Ayrı bir admin route eklemek; panel taşıma mevcut çalışma alanı yüzeyi içinde kalır.

## Dependencies

- V1-RMD-068

## Deliverables

- Türkçe metin, çeviri haritaları, yeniden konumlanmış sağlık göstergesi ve süre eşik renklendirmesi içeren mutfak çalışma alanı.

## Acceptance evidence

- `pnpm --dir src/Clients/PosTerminal typecheck`, `pnpm --dir src/Clients/PosTerminal test` ve `pnpm --dir src/Clients/PosTerminal build` sıfır çıkış kodu verir.
- Semih mutfak ekranını açar; `Unknown` kelimesinin hiçbir yerde görünmediğini, sağlık durumlarının Türkçe yazıldığını, hedef süreyi aşan biletin kırmızı vurgulandığını doğrular.

## Handoff

- V1-RMD-070
