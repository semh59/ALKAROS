# V1-RMD-666 - BuildingBlocks belge sapmaları ve açık kararların kaydı

- Task ID: V1-RMD-666
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-596 bulguları B023, B007, B021, B024, B020, B025, B026 (belge kısmı) kapanır: kodla çelişen belge cümleleri düzeltilir, bilinçli bırakılan davranışlar gerekçe ve kaldırma koşuluyla yazılır. Gelen kutusu (inbox) kodu V1-RMD-134 ile silindi, belge hâlâ çalışıyor gibi anlatıyordu; üç farklı idempotency hata kodu belgede tek kodla anlatılıyordu; "en çok 3 deneme" kuralı olay türüne özel bütçeleri anmıyordu; olay sırası garantisi yazılı değildi; ölü `inbox_messages` şeması, zarf AAD'sinin kayda bağlı olmaması, sır adı çakışması ve küçük sertleştirmeler kümesi açıkça kayıt altına alınmamıştı; `btree_gist` geri alma anlatımı göçle çelişiyordu; Npgsql lisansı izin listesinde adı geçmiyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-666-buildingblocks-docs-and-open-decisions.md`
- `evidence/V1-RMD-666/**`
- `docs/security/sensitive-envelope-known-limits.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/idempotency-inbox-outbox.md - yalnız uygulama durumu bölümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/postgresql-extension-ownership.md - yalnız geri alma anlatımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/engineering/third-party-license-policy.md - yalnız bilinen istisnalar
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-665-kitchen-item-event-atomicity.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Dört belgedeki düzeltmeler ve yeni "bilinen sınırlar" belgesi.

## Out of scope

- Kod değişikliği; `~/.claude/skills/alkaros-boundary-event` becerisi depo dışındadır (aynı inbox anlatımını içerir, ayrıca güncellenmelidir); Npgsql için ürün sahibi onayı; kullanılmayan `SecretStartupValidator` ve ölü `inbox_messages` şemasının silinmesi.

## Dependencies

- V1-RMD-665

## Acceptance evidence

1. Belge cümleleri kodla karşılaştırılarak doğrulanır (`evidence/V1-RMD-666/README.md`: her düzeltme için kod konumu).
2. plan-audit ve consistency-audit sıfır hata.

## Handoff

- None
