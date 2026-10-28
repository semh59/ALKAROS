# V1-RMD-743 - Son yönetici koruması eşzamanlı çağrıda aşılıyor (739 bulguları V1-IAM-008, V1-RMD-668)

- Task ID: V1-RMD-743
- Status: InProgress
- Assignee: claude-code-session_019vXeYYCYNP4vpjxAZb7pmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

`RoleManagementService` son yönetici korumasını önce sayarak, sonra ayrı bir çağrıyla silerek uygular (kontrol-sonra-yaz, kilitsiz). İki yönetici aynı anda birbirinin rolünü veya `roles.manage` iznini geri alırsa her ikisi de "başka yönetici kalıyor" görür, ikisi de geçer ve sistemde yönetici kalmaz. Düzeltme: sayım ve silme tek işlemde, bir PostgreSQL danışma kilidi altında yapılır; kilidi alan ikinci çağrı güncel sayımı görür ve `LastRoleManagerException` ile reddedilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-743-son-yonetici-es-zamanlilik.md`
- `evidence/V1-RMD-743/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Depoya, korumalı geri alma yöntemleri eklemek (kullanıcı-rol ve rol-izin için); servis bunları kullanır.
- İki yöneticinin aynı anda birbirini geri almasında en az bir yöneticinin kaldığını gerçek PostgreSQL ile kanıtlayan test.

## Out of scope

- Geri alınmış aktörün uçuştaki komutu tamamlaması (CODE-008 kararı, kabul edilen davranış).

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Eşzamanlı karşılıklı geri alma testi kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-743/` altındadır.
2. Mutasyon kanıtı: kilit kaldırılınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
