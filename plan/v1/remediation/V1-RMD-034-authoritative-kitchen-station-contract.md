# V1-RMD-034 - Authoritative kitchen station contract

- Task ID: V1-RMD-034
- Status: Done
- Assignee: /root
- Work type: integration
- Surface state: Existing

## Goal

Cashier submit dispatcher'ının kullandığı kitchen station kimliğini authenticated runtime configuration contract'ıyla
PosTerminal'e açmak ve production kitchen UI'ın aynı authoritative istasyonu sorgulamasını sağlamak.

## Owned surface

- `src/Host/DualScreen/DualScreenApplication.cs`
- `src/Clients/PosTerminal/src/App.tsx`
- `src/Clients/PosTerminal/src/features/kitchen-operations/kitchenApi.ts`
- `src/Clients/PosTerminal/src/features/kitchen-operations/kitchenApi.test.ts`
- `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs`
- `evidence/V1-RMD-034/**`

## In scope

- Authenticated terminal runtime configuration read contract'ı ve fail-closed station doğrulaması.
- Kitchen route'un config yüklenmeden ticket sorgulamaması; loading, unauthorized, offline ve error durumlarını bounded
  göstermesi.
- Gerçek container'da submit edilen ticket'ın aynı station UI'da görünmesi ve durum ilerletme kanıtı.

## Out of scope

- Bill split, floor-plan bootstrap, reservation alanları, merge/unmerge, katalog reflow veya dış go-live blocker'ları.
- Yeni kitchen routing, printer veya ticket domain davranışı.

## Dependencies

- V1-GOV-022
- V1-RMD-032

## Acceptance evidence

- Runtime configuration endpoint'i missing/expired session için `401`, yanlış terminal bağlamı için fail-closed sonuç ve
  geçerli cashier session için yalnız station allowlist DTO'su döndürür; secret veya environment dump yayınlamaz.
- UI hard-coded `hot-line` değerini içermez. Configuration alınmadan station-scoped ticket isteği gönderilmez; config
  hatası boş mutfak olarak gösterilmez.
- Host contract testleri, kitchen client testleri, `pnpm test`, `pnpm typecheck`, `pnpm build`, ilgili .NET testleri ve
  plan validation exit code `0` verir.
- Gerçek Compose üzerinde yeni sipariş `kitchen-main` ticket'ı üretir; `/kitchen` aynı ticket'ı gösterir ve
  `Queued → Preparing → Ready` ilerletmesi PostgreSQL'de doğrulanır.

## Handoff

- V1-GOV-004
