# V1-RMD-719 - Kasa hızlı sipariş gönderiminin bütünlüğü

- Task ID: V1-RMD-719
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-602 bulguları B002, B011 ve B027 kapanır. Kasa hızlı sipariş gönderimi başarısız olursa kasaya gönderme yapılmaz ve sepet korunur; yeniden deneme aynı gönderim kimliğiyle gider ve mutfakta tek fiş çıkar; send-to-cashier yanıtı denetlenir. Ateşlenmemiş Draft kalemler faturaya girmez veya Draft sipariş kasaya gönderilemez. Her işlemden sonra dört yenileme isteği tek isteğe iner ve hata mesajı sunucu hata koduna göre Türkçe gösterilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-719-cashier-quick-order-send-integrity.md`
- `evidence/V1-RMD-719/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/** - yalnız hızlı sipariş gönderimi ve hata mesajı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx - yalnız yenileme sıklığı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/CashierHandoffStore.cs - yalnız Draft sipariş kuralı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/Bills/Bill.cs - yalnız Draft kalem dışlama
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/** - yalnız Kasa gönderim testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/** - yalnız yeni test
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Gönderim başarısızlığında sepetin korunması, yanıt kaybında yeniden denemenin ikilememesi, Draft kalem kuralı, yenileme azaltma.

## Out of scope

- Hızlı sipariş dışındaki Kasa akışları ve ödeme.

## Dependencies

- V1-RMD-602

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-719/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Semih senaryosu: Kasada ağ koparsa sipariş ikilenmez, mutfakta tek fiş çıkar; gönderim başarısız olunca sonraki müşterinin siparişi öncekine eklenmez.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-603
