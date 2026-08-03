# V0-DOC-001 - Correct the master specification baseline

- Task ID: V0-DOC-001
- Status: Blocked
- Assignee: codex-v0-doc-001
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.0-II.15
- PDF:IV.0-IV.1
- CORR:C1
- CORR:C2
- CORR:C3
- CORR:C4
- CORR:C5
- CORR:C6
- CORR:C7
- CORR:C8
- CORR:C9

## Goal

Master PDF'deki doğrulanmış çelişki ve açık maddeleri tek revize edilebilir kaynakta düzeltmek.

## Owned surface

- `docs/specification/restaurant-pos-master.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- C1-C9 düzeltmeleri, II.16 harita hatası ve TRACEABILITY'deki kritik audit maddeleri.

## Out of scope

- Yeni ürün özelliği eklemek veya implementation ayrıntısı uydurmak.

## Dependencies

- V0-DOM-001
- V0-DOM-002
- V0-DOM-003
- V0-DOM-004
- V0-DOM-005
- V0-DOM-006
- V0-DOM-007
- V0-DOM-008
- V0-DOM-010
- V0-DAT-001
- V0-DAT-002
- V0-DAT-003
- V0-DAT-004
- V0-DAT-005
- V0-ARC-001
- V0-ARC-002
- V0-ARC-003
- V0-ARC-004
- V0-ARC-005
- V0-CMP-001
- V0-CMP-002
- V0-CMP-003
- V0-CMP-004
- V0-BKP-001
- V0-BKP-002
- V0-LIC-001

## Blocker

- Mevcut master specification gerçek olmayan `II.16` gereksinimi üretmiş ve açık Blocked dependency'lere rağmen `Done`
  olarak işaretlenmiştir. Ancak yalnız PDF'de doğrulanmış heading/Correction kayıtları ve tüm dependency kararları
  geçerli kanıtla kapandığında görev yeniden `Planned` yapılabilir.

## Deliverables

- V0-DOC-001 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Kaynak dokümanda açık critical/high finding kalmıyor; cross-reference ve count denetimi hatasız.

## Handoff

- None
