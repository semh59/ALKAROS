# V1-RMD-075 - PosTerminal kitchen and system-health workspace split

- Task ID: V1-RMD-075
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Mutfak sağlık ve yedek panelini mutfak operasyon görünümünden tamamen çıkarıp yalnızca yönetici rolüne açık ayrı bir sistem sağlığı rotasına taşımak; mutfak ekranında yalnızca üst bar durum noktası kalır ve bu nokta rotaya bağlanır. Mutfak bileti geçen süre eskalasyonu sabit eşik yerine `V1-RMD-074` sözleşmesindeki `targetPrepMinutes` alanından beslenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-075-posterminal-kitchen-and-system-health-workspace.md`
- PO:2026-09-01 kararıyla src/Clients/PosTerminal/src/features/kitchen-operations/** yüzeyi V1-RMD-082’ye devredildi; bu historical task closed kalır.
- `src/Clients/PosTerminal/src/features/system-health/**`
- `src/Clients/PosTerminal/src/App.tsx`
- `evidence/V1-RMD-075/**`

## In scope

- `HealthPanel` bileşeninin mutfak çalışma alanından çıkarılması; yerine kalan üst bar durum noktasının yönetici rotasına bağlanması.
- Yeni `system-health` özelliğinde yalnızca yönetici rolüne render edilen ayrı çalışma alanı ve `App.tsx` içinde rota tanımı.
- Mutfak bileti geçen süre rozeti eşiklerinin bilet `targetPrepMinutes` alanından hesaplanması; alan yoksa güvenli varsayılan. Bilet modeline `targetPrepMinutes` alanı eklenir.
- `App.tsx` katalog rotasında `V1-RMD-076` ürün kullanılabilirlik geri çağırmasının (`onSetAvailability`) yalnızca manager rolüne bağlanması.
- Yeni davranışların Vitest testleri.

## Out of scope

- Host tarafı sağlık veya kitchen operations API'sini değiştirmek.
- Rol atama veya kimlik doğrulama akışını değiştirmek.

## Dependencies

- V1-RMD-076

## Deliverables

- Mutfak operasyon görünümünden ayrılmış, yönetici rotasında yaşayan sistem sağlığı çalışma alanı ve sözleşmeye bağlı süre eskalasyonu.

## Acceptance evidence

- `pnpm --dir src/Clients/PosTerminal typecheck`, `pnpm --dir src/Clients/PosTerminal test` ve `pnpm --dir src/Clients/PosTerminal build` sıfır çıkış kodu verir.
- Semih; mutfak rolüyle sağlık panelini göremediğini, yönetici rolüyle ayrı rotada gördüğünü, biletin hedef süresine göre renk değiştirdiğini doğrular.

## Handoff

- V1-RMD-077
