# V1-RMD-122 - Consistency-audit Host cross-schema write rule

- Task ID: V1-RMD-122
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Bu maddeleri tamamla" — Dalga 3'ün [Boundary] kendi taze
denetiminde bulunup bilerek kapsam dışı bırakılan ikinci madde),
`tools/consistency-audit`'in rule 5'inin (cross-schema yazma denetimi)
yalnızca `src/Modules/**`'i taradığı, `src/Host/**`'i hiç taramadığı kör
noktayı kapatır — V11-RMD-002'nin kapattığı 5-modül `MODULE_SCHEMA` boşluğuyla
aynı kök neden sınıfı. Yeni rule 7, her Host Experience alanının
(Orders/Billing/Catalog/Tables/KitchenOperations/Roles/Authorization/DualScreen)
yalnız kendi bounded context'inin şemasına yazdığını doğruluyor;
`table_mgmt.tables`'ın zaten mutabık kılınmış "soft cache pointer" deseni
(Orders/Billing/DualScreen, `PostgresTablePointerProjector` ile onarılan) ve
CLI/bootstrap giriş noktası (`Program.cs`) açıkça belgelenmiş istisnalar,
kör nokta değil.

## Owned surface

- `plan/v1/remediation/V1-RMD-122-consistency-audit-host-cross-schema-rule.md` (yeni)
- `tools/consistency-audit/consistency_audit.py` (kurucu sahiplik — V11-RMD-002'nin
  de aynı dosyaya aynı gerekçeyle dokunduğu emsal)
- `docs/CONSISTENCY_AUDIT.md` (varsa, yeni rule 7'nin belgelenmesi için)

## In scope

- `HOST_AREA_SCHEMA`: Host Experience alt klasörlerinin (ve `DualScreen`'in)
  sahip olduğu tek şema; `Experience/` altında doğrudan duran bir dosya
  (paylaşılan cross-cutting yardımcı, örn. `ManagementSessionLookup.cs`)
  `identity`'nin cross-cutting yüzeyi sayılıyor (V0-ARC-001 satır 1).
- `HOST_AREA_EXTRA_SCHEMAS`: `table_mgmt.tables` pointer istisnasının açık
  allowlist'i (Orders, Billing, DualScreen).
- `HOST_EXEMPT_FILES`: `Program.cs` (composition root'un kendisi, per-request
  bir Experience store değil).
- Yeni tarama döngüsü: `src/Host/**/*.cs`'de (Program.cs hariç) her
  UPDATE/INSERT INTO/DELETE FROM hedefinin şemasını, dosyanın alanının sahip
  olduğu şema veya izinli ekstra şema veya `audit` ile karşılaştırıp aykırı
  olanı bulgu olarak raporlamak.
- Modül docstring'ine rule 7'nin tanımı eklendi.

## Out of scope

- Rule 5/6'nın kendisi veya `MODULE_SCHEMA` sözlüğü — bu görev yalnız Host
  katmanı için paralel bir kural ekliyor, mevcut modül kuralına dokunmuyor.
- `table_mgmt.tables` pointer deseninin kendisini değiştirmek veya
  kaldırmak — kasıtlı, zaten mutabık bir tasarım (V1-RMD-078/V1-TBL-007),
  bu görevin kapsamı yalnız onu doğru şekilde istisna olarak tanımak.

## Dependencies

- V1-RMD-120

## Acceptance evidence

- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı (yeni rule 7 sıfır yeni ihlal buldu — mevcut Host
  kodu zaten tanımlı istisnalarla tutarlı).
- Revert-and-confirm: `HOST_AREA_EXTRA_SCHEMAS`'tan `Experience/Orders`
  girişi geçici kaldırıldığında, `OrderManagementStore.cs:98`'in
  `table_mgmt` yazması tam olarak beklenen mesajla bulgu olarak çıktığı
  doğrulandı; girdi geri eklenince tekrar temiz.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata.

## Handoff

- V1-GOV-119
