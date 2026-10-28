# V1-RMD-745 - Müşteri ekranı kayan oturumu eşzamanlı iki istekte yanlış 401 veriyor (739 bulgusu V1-RMD-690)

- Task ID: V1-RMD-745
- Status: Done
- Assignee: claude-code-session_019vXeYYCYNP4vpjxAZb7pmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

AuthenticateDisplayAsync tek deyimde önce UPDATE ile oturumu tazeler, ardından aynı deyimdeki SELECT koluyla taze oturumu arar. İki eşzamanlı istekten biri satırı günceller; diğerinin UPDATE'i kilidi bekleyip yeniden kontrolde satır bulamaz, SELECT kolu ise deyim başındaki eski görüntüde last_seen_at'i bayat görür; sonuç null yani geçerli oturuma yanlış 401. Düzeltme: sonuç boşsa yeni bir deyimle oturumun hâlâ geçerli olup olmadığını (iptal edilmemiş, süresi dolmamış) yeniden sorgulamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-745-musteri-ekrani-oturum-yarisi.md`
- `evidence/V1-RMD-745/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- AuthenticateDisplayAsync boş sonuçta yeni deyimle doğrulama
- Satır kilidi bekleyen isteğin geçerli oturumda 401 almadığını gerçek PostgreSQL ile kanıtlayan test

## Out of scope

- Kasiyer oturumu (AuthenticateCashierAsync) davranışı

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Yeni test kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-745/` altındadır.
2. Mutasyon kanıtı: düzeltme geri alınınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
