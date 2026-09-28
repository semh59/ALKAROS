# V1-RMD-385 - PosTerminal pending-checks Tur 2 denetimi: nav'da canlı sayaç yok (büyük kapsamlı, ertelendi)

- Task ID: V1-RMD-385
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 10: PosTerminal'in
Bekleyen Hesaplar ekranı. Bu ekran zaten bu turun en gelişmiş T7 örneği — hem gerçek zamanlı bir
SignalR hub'ı (`PendingChecksChanged`) HEM DE bir 60 saniyelik yedek poll'u aynı anda taşıyor.

Tek gerçek gözlem: kalıcı gezinme çubuğunun ("Bekleyen hesaplar" nav öğesi) hiçbir canlı sayaç
göstergesi yok — bir kasiyer "Kasa satış" ekranındayken yeni bir hesap kuyruğa düştüğünde,
proaktif olarak "Bekleyen hesaplar" sekmesine gitmedikçe bunu öğrenemiyor. `ShellNavigationItem`
arayüzünde bir `badge`/`count` alanı bile yok.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-385-pending-checks-round2-audit.md`

## In scope

- Yok — kod değişikliği yapılmadı.

## Out of scope

- **[T7, rol-arası haberleşme — büyük kapsamlı] Nav'da canlı bekleyen-hesap sayacı yok.**
  Düzgün bir çözüm: (1) `ShellNavigationItem`'a bir `badge` alanı eklemek, (2)
  `ProductionShell`'i bunu gösterecek şekilde genişletmek, (3) şu an yalnızca
  `PendingChecksWorkspace` bileşeni MOUNT olduğunda (yani zaten o rotadayken) bağlanan hub/poll
  mantığını `ExperiencePage` seviyesine taşımak (böylece kasiyer BAŞKA bir ekrandayken de canlı
  kalır). Bu, paylaşılan kabuk altyapısını etkileyen bir değişiklik — tek bir modülün denetim
  turunda aceleye getirilecek kadar dar kapsamlı değil, Semih'in kararına bırakıldı.

## Dependencies

- None

## Acceptance evidence

- Kod değişikliği yapılmadığı için test/mutation-check gerekmedi.

## Handoff

- None
