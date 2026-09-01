# V1-RMD-090 - Seating tolerates stale table version

- Task ID: V1-RMD-090
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-01
- PDF:II.2.20

## Goal

`DualScreenStore.StartOrderAsync` içinde masaya oturtma, istemci güncelliğini yitirmiş bir `ExpectedTableRowVersion` gönderdiğinde masa hâlâ `Available`, aktif adisyonsuz ve siparişsiz olsa bile `DualScreenConflictException("Table row version is stale.")` fırlatıyor. Bu, sahada garsonun geçerli bir oturtmada anlamsız "yenile" hatası görmesine yol açar. Sürüm eşitlik kapısı kaldırılır; oturtma kararı `FOR UPDATE` kilidi altında okunan taze duruma ve zaten mevcut niyet kontrollerine (`tableActive`, `current_bill_id`, mevcut sipariş durumu, `current_status = 'Available'` ve son atomik `UPDATE ... WHERE current_status = 'Available' AND current_order_id IS NULL AND current_bill_id IS NULL`) dayanır. Gerçek çakışmalar (masa pasif, adisyon açılmış, başka terminalde aktif sipariş, masa artık `Available` değil) spesifik hatalarla reddedilmeye devam eder.

## Owned surface

- `plan/v1/remediation/V1-RMD-090-seating-tolerates-stale-table-version.md`
- `src/Host/DualScreen/DualScreenStore.cs`
- `tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs`
- `evidence/V1-RMD-090/**`

## In scope

- `StartOrderAsync` içindeki `request.ExpectedTableRowVersion is { } expected && expected != tableRowVersion` sürüm eşitlik kapısını kaldırmak; parametre sözleşmede kalır (API uyumu) ve pozitif-değer/masasız doğrulaması korunur, ama gating amaçlı kullanılmaz.
- `DualScreenStoreTests.TableBoundOrderRejectsStaleOrBusyTablesWithoutCreatingAnotherOrder` testini yeni davranışa göre güncellemek: güncelliğini yitirmiş sürüm + oturtulabilir masa → oturtma başarılı, tam bir sipariş oluşur; güncelliğini yitirmiş sürüm + başka terminalde aktif masa → yine `DualScreenConflictException`.
- Güncelliğini yitirmiş sürümle gelen oturtmanın oturtulabilir masada başarılı olduğunu doğrulayan yeni bir test.

## Out of scope

- `/tables/{tableId}/status`, zemin planı kaydetme, reservations ve table transfer yolları.
- Sözleşmeden `ExpectedTableRowVersion` parametresini kaldırmak.
- Frontend değişikliği; sunucu artık uyumlu oturtmalarda 409 döndürmediği için istemci değişmeden çalışır.

## Dependencies

- V1-GOV-056

## Deliverables

- Güncellenmiş `src/Host/DualScreen/DualScreenStore.cs` ve `DualScreenStoreTests.cs`.
- `evidence/V1-RMD-090/` altında öncesi/sonrası davranış ve test çıktısı.

## Acceptance evidence

- `dotnet test tests/Host/MigrationComposition` sıfır hata verir.
- Güncelliğini yitirmiş `ExpectedTableRowVersion` ile oturtulabilir masaya oturtma başarılı olur ve tam bir Draft sipariş + masa işaretçileri oluşur.
- Masa pasif / adisyonlu / başka terminalde aktif olduğunda oturtma yine spesifik `DualScreenConflictException` ile reddedilir.

## Handoff

- V1-GOV-057
