# V1-RMD-042 - Shell freshness accessibility and WCAG

- Task ID: V1-RMD-042
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Production shell'deki daimi "fresh" durum hatasını düzeltmek; bağlantı koptuğunda doğru çevrimdışı durumunu yansıtmak; viewport zoom kısıtlamalarını kaldırmak; klavye navigasyonu, focus trap, Escape tuşu ve min 44x44 dokunma hedeflerini tamamlayarak WCAG 2.2 AA erişilebilirlik gereksinimlerini sağlamak.

## Owned surface

- `src/Clients/PosTerminal/src/shell/ProductionShell.tsx`
- `src/Clients/PosTerminal/src/styles.css`
- `src/Clients/WaiterPwa/wwwroot/waiter-app.css`
- `evidence/V1-RMD-042/**`

## Dependencies

- V1-CUI-005
- V1-RMD-038
- V1-RMD-039
- V1-RMD-041

## Acceptance evidence

- Çevrimdışı durumda freshness göstergesi asla "fresh" kalmaz; gerçek bağlantı durumunu yansıtır.
- Viewport kullanıcı zoom'unu engellemez.
- Modal focus trap ve Escape tuşu yönetimi çalışır.

## Handoff

- V1-RMD-043
