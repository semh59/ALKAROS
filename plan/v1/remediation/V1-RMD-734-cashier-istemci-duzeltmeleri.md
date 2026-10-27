# V1-RMD-734 - Kasa istemcisi düzeltmeleri (603 B007, B032, B033, B036)

- Task ID: V1-RMD-734
- Status: InProgress
- Assignee: claude-code-session_019auoHryukF9ryWZPtnjznR
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B007 (kart kilidi uyarısında tutar ve müdüre yönlendirme), B032, B033, B036: tutar alanı kalanla yenilenmiyor; yoklama odak kaybettiriyor; yöntem çipleri 48 px altı; fetch zaman aşımı yok ve 5xx'te idempotency anahtarı yenileniyor; cash-session ve customer-accounts girdi kaybı ve eski yanıt ezmesi; hata mesajlarında yapılacak iş ve yenile düğmesi yok; outline:none; izne göre düğme gizleme yok; saat dilimi.

## Owned surface

- `plan/v1/remediation/V1-RMD-734-cashier-istemci-duzeltmeleri.md`
- `evidence/V1-RMD-734/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Cashier split-payment, cash-session, customer-accounts sayfaları ve StaticApps testleri; axe testi.

## Out of scope

- Bilgi fişi uç noktasının hata zarfı (404) V1-RMD-733 kapsamındadır; bu görev ona dokunmaz.
- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-734/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
