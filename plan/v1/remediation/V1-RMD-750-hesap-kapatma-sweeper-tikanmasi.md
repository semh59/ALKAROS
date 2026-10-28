# V1-RMD-750 - Hesap kapatma süpürücüsü kapanamayan hesaplarda tıkanıyor (kod incelemesi bulgusu)

- Task ID: V1-RMD-750
- Status: Done
- Assignee: claude-code-session_019vXeYYCYNP4vpjxAZb7pmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

BillClosureSweeper her dakika ödemesi grace süresini aşmış, Paid/Cancelled olmayan en eski 200 hesabı `updated_at` artan sırayla seçer. IBillClosureService.TryCloseAsync `NotClosable` veya `NotSatisfied` döndüğünde hesabı değiştirmez; kısmen ödenmiş ve bilerek açık bırakılmış hesap her turda listeye geri girer. Bu hesapların sayısı 200'e ulaşırsa sonradan kapanabilir hale gelen yeni hesaplar sıraya hiç giremez ve ödenmiş hesap açık kalır. Süpürücü, kapanamayan hesaplar yüzünden kapanabilir hesapları aç bırakmayacak biçimde seçim yapmalıdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-750-hesap-kapatma-sweeper-tikanmasi.md`
- `evidence/V1-RMD-750/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillClosureSweeper.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Aday seçiminin kapanamayan eski hesaplarla tıkanmaması (en güncel ödeme hareketi önce gelecek sıralama)
- 200'den fazla kapanamayan hesap varken kapanabilir yeni hesabın kapandığını gösteren test

## Out of scope

- BillClosureService kapanma kuralları
- Süpürücü aralığı, grace süresi ve toplu iş boyutu

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Yeni test kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-750/` altındadır.
2. Mutasyon kanıtı: sıralama düzeltmesi geri alınınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
