# V1-RMD-078 - Table metadata field-level merge

- Task ID: V1-RMD-078
- Status: NotApplicable
- Assignee: Semih (product owner)
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Masa satır-sürüm çakışmasında, çakışan alanlar farklıysa otomatik alan bazlı birleştirme yaparak "yenile" hatasını azaltmak.

## Owned surface

- `plan/v1/remediation/V1-RMD-078-table-metadata-field-level-merge.md`
- `evidence/V1-RMD-078/**`

## In scope

- Masa mutasyonlarında disjoint alan kümesi tespiti ve güvenli otomatik birleştirme.

## Out of scope

- Durum geçişi, sipariş veya adisyon işaretçisi alanlarının birleştirilmesi.

## Dependencies

- V1-GOV-040

## Deliverables

- Güvenli alan bazlı birleştirme veya uygulanamazlık kararı.

## Acceptance evidence

- Karar kimliği: PO:2026-08-31 (Semih onayı). `table_mgmt.tables` şeması yalnızca `table_id`, `zone_id`, `table_number`, `capacity`, `active`, `current_status`, `current_order_id`, `current_bill_id`, `row_version` sütunlarını taşır; serbest metin not veya etiket alanı yoktur. Eşzamanlı düzenlenebilen alanlar durum geçişi ve işaretçi alanlarıyla sınırlıdır ve bunlar V1 doğruluğu için fail-closed kalmalıdır; tek metadata alanı `capacity` olup ayrı bir birleştirme yolu değeri yoktur, `table_number` benzersizlik kısıtı nedeniyle birleştirilemez. Bu nedenle görev `NotApplicable` kapanır; mevcut optimistic-locking "yenile" davranışı korunur. Serbest metin masa notu eklenirse bu karar yeniden değerlendirilir.
- Ek gerekçe (PO:2026-09-01, 17. dalga). Generic column-level otomatik birleştirme, transactional durum makinesi ve para verisinde sektörde kullanılan bir yaklaşım değildir; bilinen bir anti-pattern'dir (iki alan yazımı, hiçbir yazarın kastetmediği ve invariant ihlal eden bir duruma birleştirilebilir; DB teorisinde write skew). Restoran POS sistemleri bu problemi sahiplik + devir, append-only sipariş satırları, türetilmiş masa durumu ve split/merge için explicit `FOR UPDATE` işlemleriyle çözer. `table_mgmt.tables` işaretçi sütunları zaten türetilmiş cache olarak ele alınır ve `ITablePointerProjector.RebuildTablePointersAsync` kilit altında gerçek kaynaktan yeniden hesaplar. Sahadaki asıl friction — oturtmada güncelliğini yitirmiş sürümde anlamsız "yenile" hatası — `V1-RMD-090` ile kilit altında taze durum niyet doğrulamasıyla giderilmiştir; bu, sektör pratiğiyle hizalı doğru çözümdür. Field-merge yaklaşımı bu görevde uygulanmaz olarak kalır.

## Handoff

- V1-GOV-041
