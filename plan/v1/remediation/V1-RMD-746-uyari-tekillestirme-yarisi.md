# V1-RMD-746 - Aynı kaynak veya hata için iki eşzamanlı ilk uyarı iki aktif kayıt açıyor (739 bulgusu V1-ALT-001)

- Task ID: V1-RMD-746
- Status: InProgress
- Assignee: claude-code-session_019vXeYYCYNP4vpjxAZb7pmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

RaiseAlertAsync, aktif uyarıyı SELECT ... FOR UPDATE ile arar ama hiç satır yoksa kilitlenecek bir şey olmadığından iki eşzamanlı ilk çağrı da boş görür ve ikisi de yeni uyarı açar; idx_alerts_dedup_active benzersiz değildir. Sonuç: 'aynı kaynak/hata = bir aktif uyarı' kuralı bozulur. Düzeltme: işlem başında tekilleştirme anahtarına (veya tür ve kaynak üçlüsüne) bağlı bir PostgreSQL danışma kilidi almak; kilidi alan ikinci çağrı ilkinin kaydını görür ve tekilleşir. Migration gerekmez (mevcut yinelenen kayıtlar benzersiz indeksi bozardı).

## Owned surface

- `plan/v1/remediation/V1-RMD-746-uyari-tekillestirme-yarisi.md`
- `evidence/V1-RMD-746/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Observability/AlertFoundation/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Observability/AlertFoundation/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- RaiseAlertAsync işleminde tekilleştirme anahtarı için danışma kilidi
- Eşzamanlı çağrılarda tek aktif uyarı kaldığını gerçek PostgreSQL ile kanıtlayan test

## Out of scope

- Benzersiz kısmi indeks migration'ı

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Yeni test kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-746/` altındadır.
2. Mutasyon kanıtı: düzeltme geri alınınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
