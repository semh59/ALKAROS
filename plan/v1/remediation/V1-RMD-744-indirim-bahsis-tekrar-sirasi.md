# V1-RMD-744 - İndirim ve bahşiş tekrarı durum kontrolünden sonra bakılıyor (739 bulgusu V1-RMD-729)

- Task ID: V1-RMD-744
- Status: InProgress
- Assignee: claude-code-session_019vXeYYCYNP4vpjxAZb7pmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

İndirim ve bahşiş uç noktaları, aynı IdempotencyKey ile gelen tekrarı (replay) hesap durumu kontrolünden sonra arar. Başarılı bir indirim veya bahşişten sonra hesap Paid/PartiallyPaid olursa, ağ zaman aşımı sonrası aynı isteğin tekrarı ilk yanıtı almak yerine 409 (desteklenmeyen hesap durumu) alır; istemci işlemin uygulanıp uygulanmadığını bilemez. Düzeltme: tekrar aramasını durum kontrolünden önce yapmak; yeni anahtar yine durum kontrolüne tabi kalır.

## Owned surface

- `plan/v1/remediation/V1-RMD-744-indirim-bahsis-tekrar-sirasi.md`
- `evidence/V1-RMD-744/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- ApplyDiscountAsync ve ApplyTipAsync içinde replay aramasını IsDiscountable kontrolünden önceye almak
- Hesap ödendikten sonra aynı anahtarla tekrarın ilk sonucu döndüğünü, yeni anahtarın 409 aldığını kanıtlayan HTTP testleri

## Out of scope

- Replay sonucunun özetini ödeme sonrası duruma göre yeniden yorumlamak

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Yeni test kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-744/` altındadır.
2. Mutasyon kanıtı: düzeltme geri alınınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
