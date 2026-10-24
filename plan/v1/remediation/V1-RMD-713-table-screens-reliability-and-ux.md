# V1-RMD-713 - Masa ekranlarının güvenilirliği ve sadeliği

- Task ID: V1-RMD-713
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B009, B010, B011, B012, B017, B018, B019, B020, B028 ve istemci Düşük kalemleri kapanır. Yoklama ekranı yeniden yüklemez ve kat planı taslağını ile açık modalı korur; rezervasyon istasyonu eylem yapabilir; eylem hataları modal içinde ve doğru çakışma tespitiyle gösterilir; garson masa yaşı rozeti çalışır; liste hatası boş liste olmaz ve hata sonrası yoklama toparlanır; hedef listeleri duruma göre süzülür ve gerekçe hazır seçenekle gelir; kontrast, yazı boyutu ve teknik jargon düzelir.

## Owned surface

- `plan/v1/remediation/V1-RMD-713-table-screens-reliability-and-ux.md`
- `evidence/V1-RMD-713/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx - yalnız masa yoklaması ve hata gösterimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/ReservationStation.tsx - yalnız yetki ve hata durumu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/screens/** - yalnız masa listesi ve transfer
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/WaiterPwa/Frontend/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-601-tables-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yoklama, taslak ve modal korunması, rezervasyon istasyonu, hata gösterimi, PWA listesi, hedef süzme ve gerekçe, kontrast ve metinler.

## Out of scope

- Sunucu düzeltmeleri (V1-RMD-710, V1-RMD-711, V1-RMD-712).

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-713/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602
