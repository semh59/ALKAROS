# V1-RMD-091 - Operational data housekeeping sweep

- Task ID: V1-RMD-091
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-01
- EXT:POSTGRESQL-18.4

## Goal

Host'a `housekeeping` CLI fiili eklenir; süresi dolmuş `idempotency_keys` satırları ve süresi dolmuş veya uzun süredir iptal edilmiş `identity.device_sessions` satırları (bunların `identity.session_operations` çocukları `ON DELETE CASCADE` ile birlikte) tek bir transaction içinde silinir. Bu tablolar süresi dolduktan sonra operasyonel değer taşımaz ve sınırsız büyür. Silinen satır sayıları raporlanır. KVKK kişisel veri saklama bu görevin kapsamı dışındadır.

## Owned surface

- `plan/v1/remediation/V1-RMD-091-operational-data-housekeeping-sweep.md`
- PO:2026-09-01 kararıyla src/Host/Program.cs yüzeyi V1-RMD-094'e devredildi; bu historical task closed kalır ve kvkk-retention fiili 21. dalgada eklenir.
- `tests/Host/MigrationComposition/Program/HousekeepingTests.cs`
- `evidence/V1-RMD-091/**`

## In scope

- `src/Host/Program.cs` içine `housekeeping --db-url <url> [--grace-days <N>]` fiili (varsayılan grace 7 gün, `ALKAROS_DB_PASSWORD` ortam değişkeninden parola).
- Tek transaction içinde `DELETE FROM idempotency_keys WHERE expires_at < now();` ve `DELETE FROM identity.device_sessions WHERE expires_at < now() - grace OR (revoked_at IS NOT NULL AND revoked_at < now() - grace);`.
- Silinen satır sayılarının ve süresinin `stdout`'a yazılması, çıkış kodu 0.
- `compose.yaml` içine yalnızca `ops` profilinde çalışan `housekeeping` servisi (digest-pinned runtime imajı).
- `docs/operations/data-housekeeping.md`: neyin silindiği, neden güvenli (PII yok, yasal saklama süreli iş kaydı yok, yalnızca süresi dolmuş kısa ömürlü operasyonel satırlar), nasıl zamanlanacağı ve KVKK PII saklamanın ayrı olduğu (`V15-KVK-001`).
- `HousekeepingTests`: süresi dolmuş satırların silindiğini, süresi dolmamış ve grace penceresi içindeki satırların korunduğunu doğrulayan testler.

## Out of scope

- KVKK kişisel veri saklama/anonimleştirme; `V15-KVK-001`/`V15-KVK-002`.
- `orders`/`bills`/fiscal/invoice gibi yasal saklama süreli tablolar.
- Host içinde periyodik `BackgroundService`; fiil cron ile zamanlanır.

## Dependencies

- V1-GOV-058

## Deliverables

- `src/Host/Program.cs` `housekeeping` fiili ve `HousekeepingTests`.
- `compose.yaml` `ops` profili `housekeeping` servisi ve `docs/operations/data-housekeeping.md`.
- `evidence/V1-RMD-091/` altında fiil çıktısı ve test sonucu.

## Acceptance evidence

- `dotnet ALKAROS.Host.dll housekeeping --db-url ...` süresi dolmuş satırları siler, sayıları raporlar, çıkış kodu 0.
- `dotnet test` housekeeping testleri: süresi dolmuş satır silinir, taze/grace-içi satır korunur.
- `docs/operations/data-housekeeping.md` KVKK ayrımını açıkça belirtir.

## Handoff

- V1-GOV-059
