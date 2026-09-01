# V1-RMD-094 - KVKK retention anonymization job

- Task ID: V1-RMD-094
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-01
- PDF:I.30-I.33
- PDF:II.11-II.12

## Goal

Host'a `kvkk-retention` CLI fiili eklenir; `V0-CMP-003` envanterindeki, V1 kapsamında verisi bulunan kişisel veri sınıfları için saklama süresi geçmiş kayıtları anonimleştirir. Sınıflar: pasif personel kaydı (`identity.users`, istihdam + 1 yıl), sipariş serbest metin notu (`orders.orders.notes`, `orders.order_items.notes`, 5 yıl) ve rezervasyon serbest metin gerekçesi (`table_mgmt.table_reservations.reason`/`release_reason`, 5 yıl). Denetim olayı (`audit.audit_events`) AUD-01 tetikleyicisi ile append-only olduğu için yerinde anonimleştirilemez; bu sınıf kapsam dışıdır ve partition-drop yöntemiyle `V15-KVK-002`'ye devredilmiştir. Fiscal/fatura/finansal sütunlar hiç dokunulmaz. Fiil varsayılan olarak dry-run çalışır; veriyi değiştirmek için `--apply` gerekir. Idempotenttir (zaten anonimleştirilmiş satırı atlar) ve `--exclude-order-ids-file` ile legal-hold listesini korur.

## Owned surface

- `plan/v1/remediation/V1-RMD-094-kvkk-retention-anonymization-job.md`
- `src/Host/Program.cs`
- `tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs`
- `docs/compliance/kvkk-retention-runbook.md`
- `evidence/V1-RMD-094/**`

## In scope

- `src/Host/Program.cs` `kvkk-retention --db-url <url> [--apply] [--as-of <ISO date>] [--exclude-order-ids-file <path>]` fiili; parola `ALKAROS_DB_PASSWORD`'dan; tek transaction; sınıf başına anonimleştirilen satır sayısı raporu.
- Personel: `active = false AND updated_at < as_of - interval '1 year'` → `username`, `display_name` maskele, `email`/`phone` NULL, `password_hash` kullanılamaz sabit değer.
- Sipariş/kalem notu: `status IN ('Completed','Cancelled','Rejected') AND created_at < as_of - interval '5 years'` ve legal-hold listesinde olmayan → `notes = '[anonymized]'`.
- Rezervasyon: `status IN ('Claimed','Cancelled','Expired') AND reserved_at < as_of - interval '5 years'` → `reason`/`release_reason` maskele.
- `docs/compliance/kvkk-retention-runbook.md`: sınıf tablosu, alanlar, saklama süreleri, anonimleştir/sil/yasal-sakla ayrımı, denetim olayının append-only olduğu ve neden kapsam dışı kaldığı, legal-hold, dry-run, cron önerisi.
- `KvkkRetentionTests`: eski/taze/hold kayıtları seed edip dry-run'ın hiçbir şeyi değiştirmediğini, `--apply`'ın eskiyi anonimleştirip tazeyi ve hold'u atladığını ve ikinci çalıştırmanın 0 değişiklik yaptığını doğrular.

## Out of scope

- Fiscal fiş, Z raporu, fatura, finansal sütunlar; hiç dokunulmaz.
- `audit.audit_events` yerinde anonimleştirmesi; AUD-01 append-only tetikleyicisi `UPDATE`/`DELETE`'i reddeder. 10 yıllık imha partition-drop gerektirir ve `V15-KVK-002`'ye devredilmiştir; `IAuditSanitizer` yazma anında sırları/PII'yi zaten maskeler.
- Provider payload retention (`V15-SEC-003`).
- Çoklu mağaza resumable checkpoint workflow'u (`V15-KVK-002`).

## Dependencies

- V1-GOV-064

## Deliverables

- `src/Host/Program.cs` `kvkk-retention` fiili ve `KvkkRetentionTests`.
- `docs/compliance/kvkk-retention-runbook.md`.
- `evidence/V1-RMD-094/` altında fiil çıktısı (dry-run + apply) ve test sonucu.

## Acceptance evidence

- `dotnet ALKAROS.Host.dll kvkk-retention --db-url ...` dry-run varsayılanı hiçbir şeyi değiştirmez ve sınıf başına sayı raporlar; `--apply` eskiyi anonimleştirir.
- `dotnet test` `KvkkRetentionTests`: 3/3 geçti — dry-run 0 değişiklik, apply eskiyi anonimleştirir + tazeyi/aktifi/yanlış-statüyü/hold'u atlar, ikinci apply 0 değişiklik; `--db-url` eksik ve bilinmeyen argüman fail-closed (exit 2).
- `docs/compliance/kvkk-retention-runbook.md` sınıf, saklama süresi, yasal-sakla ayrımını ve denetim olayının kapsam dışı gerekçesini içerir.

## Handoff

- V1-GOV-065
