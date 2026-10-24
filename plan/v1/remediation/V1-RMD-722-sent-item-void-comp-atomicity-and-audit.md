# V1-RMD-722 - Gönderilmiş kalem iptali ve ikramın atomikliği ve izi

- Task ID: V1-RMD-722
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-602 bulguları B009, B010, B013, B014, B024 ve B025 kapanır. Gönderilmiş kalem iptalinde mutfak fişi ve hesap adımları hata verirse onarım yolu olur ve ikinci denetim olayı aynı işlemde yazılır; void-sent ve ikram idempotency anahtarı alır, kaybolan yanıtın yeniden denemesi ilk cevabı döndürür; ikram tamamlanmış/iptal siparişe ve ödenmiş faturaya reddedilir; kişisel ikram bütçesi İstanbul iş gününe göre sıfırlanır; transfer-server, fire-course, submit-draft ve başka garsona atama denetim kaydı yazar; sipariş okuma uçlarına sahiplik/rol süzgeci eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-722-sent-item-void-comp-atomicity-and-audit.md`
- `evidence/V1-RMD-722/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/** - yalnız iptal, ikram, devir ve okuma yetkisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/** - yalnız ikram kuralı ve bütçe günü
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/** - yalnız yeni ve düzeltilen test
- `database/migrations/V1/V1-RMD-722/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json - yalnız bu uçların çıkarılması
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Atomiklik/onarım, idempotency, ikram kuralları, bütçe günü, denetim izi ve okuma yetkisi. B014 gün sınırı PO kararıdır; para/hukuk etkisi olmadığı için İstanbul iş günü uygulanır ve karar kayda geçer.

## Out of scope

- NFC ucundaki istisna eşlemesi ve hız sınırı V1-RMD-721'de ele alınır.
- Sahiplik süzgecinin ürün kararı gerektiren kısmı (tüm garsonların tüm masaları görmesi) PO sorusu olarak kalır.

## Dependencies

- V1-RMD-602

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-722/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Semih senaryosu: gönderilmiş kalem iptalinde onay istenir, stok geri döner, ağ kopup yeniden denenince tek iptal ve tek denetim kaydı oluşur.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-603
