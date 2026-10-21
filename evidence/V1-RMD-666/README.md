# V1-RMD-666 — BuildingBlocks belge sapmaları ve açık kararlar

Kapatılan bulgular (belge/karar kaydı): 596-B023, B007, B021, B024, B020, B025, B026 ve B012'nin "üretimde bağlı değil" kısmı.

Her düzeltmenin koddaki dayanağı:
- Gelen kutusu silindi, `inbox_messages` ölü şema: `RetryPolicy.cs` yorumu (AllowedTableNames yalnız `outbox_messages`), V1-RMD-134 kaydı; B024 silinmedi, kaldırma ayrı göç kararı olarak belgede yazılı.
- İdempotency hata kodları: `ApiErrorCatalog.cs:563` (IDEMPOTENCY_CONFLICT), `OrderManagementEndpoints.cs:1154`, `NfcOrderingEndpoints.cs:186`, `BillingSplitApplication.cs:590` (IDEMPOTENCY_KEY_REUSED), `ApiErrorCatalog.cs:580` (TENDER_IDEMPOTENCY_KEY_REUSED); `IDEMPOTENCY_KEY_CONFLICT` yalnız `SubmitOrderRequestHash.cs:19` yorumunda.
- Deneme bütçesi ve kira/zaman aşımı sayımı: `OutboxStore.cs` (V1-RMD-661), `RetryPolicy.cs`; olay türüne özel profil: `OutboxRetryProfile`, Yemeksepeti durum eşitleme kaydı.
- Sıra ve temizlik: `OutboxStore.ClaimPendingAsync` (`ORDER BY created_at`), `OutboxStore.PurgeAsync` (14/90 gün).
- `btree_gist` geri alma: `database/migrations/V1/V1-FND-021/012-btree-gist-ownership.down.sql` yalnız doğrulama yapar, DROP içermez.
- Lisanslar: Npgsql PostgreSQL Lisansı ve lisans ifadesiz 4 paket belgede "ürün sahibi kararı bekliyor" olarak kayıtlı.
- B020/B025/B026 ve üretimde bağlı olmayan yetenekler ile bayat `project-manifest.json`: yeni `docs/security/sensitive-envelope-known-limits.md`.

## Doğrulanamadı / depo dışı
- `~/.claude/skills/alkaros-boundary-event/SKILL.md` aynı inbox anlatımını içeriyor; depo dışı olduğu için bu görevde değiştirilmedi.
- Npgsql lisansının izin listesine alınması ürün sahibi kararıdır, verilmedi.

## Kapanış
plan-audit 0 hata 0 uyarı, consistency-audit clean.
