# V0-CMP-003 - Create KVKK data inventory

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Tüm modüllerdeki kişisel veriyi, hukuki amacı, saklama süresini ve imha/anonymization yöntemini envanterlemek.

## Owned surface

- `evidence/v0/compliance/V0-CMP-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Customer, user, order notes, raw provider payloads, audit, fiscal, invoice, supplier ve device data.

## Out of scope

- Anonymization kodunu uygulamak.

## Dependencies

- V0-ARC-001

## Deliverables

- V0-CMP-003 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Her PII alanının owner, purpose, retention, access role ve disposal action değeri dolu; sahipsiz PII yok.

## Handoff

- V13-CST-001 ve V15-KVK-001.

