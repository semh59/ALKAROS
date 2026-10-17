# V1-RMD-592 - Son değişikliklerin Docker yığınında uçtan uca doğrulanması

- Task ID: V1-RMD-592
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Bekçi konteyneri, eski ölçüm işareti, saklama temizliği ve yazıcı ekleme formu yalnız testlerle ve (bekçi için) kısmi Docker koşusuyla doğrulandı; çalışan yığın hâlâ eski imajdaydı ve 193 numaralı göç çalışan veritabanına uygulanmamıştı. Yığın güncel koddan yeniden kurulur ve her değişiklik gerçek veritabanı, API ve tarayıcıyla denenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-592-docker-end-to-end-proof.md`
- `evidence/V1-RMD-592/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-591-posterminal-test-timeout.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yığının api, web, migrate ve bekçi imajlarının güncel koddan yeniden kurulması (veritabanı birimi korunur); göç 193'ün uygulanması; yazıcı ekleme ucu ve ekranı; eski satırların başlangıçtaki saklama temizliğiyle silinmesi; eski ölçüm işaretinin ekranda görünmesi; bekçinin API yeniden başlarken davranışı.

## Out of scope

- Kod değişikliği: bulunan kusurlar ayrı görev olarak açılır.

## Dependencies

- V1-RMD-591

## Acceptance evidence

1. Docker çıktısı ve ekran görüntüleri; bulgular README'de.
2. Çıktılar `evidence/V1-RMD-592/` altındadır.

## Handoff

- V1-RMD-593
