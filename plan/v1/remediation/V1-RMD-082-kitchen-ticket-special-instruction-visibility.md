# V1-RMD-082 - Kitchen ticket special instruction visibility

- Task ID: V1-RMD-082
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Sipariş kaleminin serbest metin özel talimatını (örn. "az", "acısız", "ekmek ayrı") mutfak fişinde göstermek. `KitchenTicketItem` zaten notu taşır; kitchen operations okuma sözleşmesi ve PosTerminal mutfak kartı bu alanı döndürüp render etmez, bu eksik giderilir. Kaleme garson tarafından girilen not, V1-RMD-019 veri-minimizasyon değerlendirmesinde "DTO'dan çıkmamalı" olarak ele alınmıştı; PO:2026-09-01 kararıyla bu alan artık mutfağa açık bir hazırlık talimatı olarak yeniden sınıflandırılır (ödeme/müşteri/personel gibi hassas alanlar kitchen DTO'sundan hariç kalmaya devam eder).

## Owned surface

- `plan/v1/remediation/V1-RMD-082-kitchen-ticket-special-instruction-visibility.md`
- `src/Host/Experience/KitchenOperations/**`
- `src/Clients/PosTerminal/src/features/kitchen-operations/**`
- `tests/Host/Experience/KitchenOperations/**`

## In scope

- `KitchenTicketItemV1` okuma DTO'suna `notes` alanının eklenmesi ve `KitchenOperationsStore.ToDto` içinde eşlenmesi.
- PosTerminal `KitchenTicketItem` istemci modeline `notes` alanının eklenmesi ve mutfak bilet kartında modifier satırının yanında ayrı bir talimat satırı olarak gösterilmesi.
- Yeni davranışın xUnit ve Vitest testleri.

## Out of scope

- Sipariş girişine not eklemek; bu `V1-RMD-083` görevine aittir.
- Mutfak fişi baskı biçimini değiştirmek.

## Dependencies

- V1-GOV-044

## Deliverables

- `notes` alanını döndüren kitchen operations sözleşmesi ve talimatı gösteren mutfak bilet kartı.

## Acceptance evidence

- `dotnet test` host kitchen operations testleri sıfır hata verir.
- `pnpm --dir src/Clients/PosTerminal typecheck`, `test` ve `build` sıfır çıkış kodu verir.
- Semih; notu olan bir kalemi mutfak ekranında açtığında talimatın kalem satırında göründüğünü doğrular.

## Handoff

- V1-RMD-083
