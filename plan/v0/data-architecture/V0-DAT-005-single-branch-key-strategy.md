# V0-DAT-005 - Resolve single-branch and business key strategy

- Task ID: V0-DAT-005
- Status: Done
- Assignee: codex-v0-dat-005
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:I.1.5
- PDF:II.0
- PDF:III.2

## Goal

Tek şubeli ürün kararı ile gelecekteki çok şube hazırlığı için opsiyonel `business_id` kullanımı arasındaki çelişkiyi
çözmek.

## Owned surface

- `docs/data/business-scope-key-strategy.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kurulum, iş ve şube kimliği, benzersiz anahtar kapsamı ve gelecekteki migration sınırı.

## Out of scope

- Çok kiracılı SaaS tasarımı veya çok dallı özellik uygulaması.

## Dependencies

- V0-ARC-001

## Deliverables

- V0-DAT-005 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Her benzersiz anahtarın açık bir kapsamı vardır; hiçbir table kullanılmamış isteğe bağlı kiracı anahtarı taşımaz; tek
  dallı değişmez uygulanabilir.

## Handoff

- GATE-V0-EXIT
