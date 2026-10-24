# V1-RMD-725 - Orders düşük önemli kalan bulgular (B031 kod kalemleri)

- Task ID: V1-RMD-725
- Status: InProgress
- Assignee: claude-code-session_01XYCVWVTbShZKMmSzdAGR6K
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-602 B031'in kod gerektiren alt kalemleri tek tek kodla yeniden doğrulanır ve gerçekse düzeltilir, değilse gerekçesiyle kapatılır. Kalemler: vardiya devrinde SubmitOrderRequestHash.ChangedBy yüzünden 409; ItemSnapshot seat ve course karşılaştırmaz; durum geçmişi sıralamasında tie-break yok; üç ayrı denetim yazma yolu; CHECK tutarlılığı; SaveAsync gidiş-dönüş sayısı; 151 tam tarama; çift dispatcher kaydı ve yanlış yorum; CHECK_NOT_RECALLABLE ham mesajı; transfer-server hedef doğrulaması ve devir notunun doğrulamadan önce yazılması; gövdede OrderId; OperationId uzunluk sınırı; kullanılmayan parametreler; CancelItem updated_at; 22 parametreli kurucu kopyası; bekleyen liste LIMIT; OperationCanceled'in 500 olması; yapılandırma InvalidOperation'ın 409 olması; ham error.message gösterimi; NfcOrder diyaloğunda odak tuzağı, Esc ve hata bandı konumu; sabit hex renkler ve metin glifleri; Kasa'da İngilizce marka metni ve sabit "Kasiyer"; yardım "Gördüm" hatasının sessiz geçmesi; Kaldır için geri alma yok; modifier maxSelections=0 tutarsızlığı ve uzunluk sınırı; pending-checks yenileme yarışı; deadlock yeniden deneme logu; geri yükleme sonrası Orders uzlaştırma adımı, kayıp ölü olay türü ve runbook, kuyruk yaşı alarmı (operasyon runbook kalemleri).

## Owned surface

- `plan/v1/remediation/V1-RMD-725-orders-low-severity-leftovers.md`
- `evidence/V1-RMD-725/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/nfc-order.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html - yalnız İngilizce başlık metni
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/operations/** - yalnız Orders geri yükleme ve alarm kalemleri
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Yukarıdaki kalemlerin kodla yeniden sayımı; gerçek olanların testle düzeltilmesi. Kod yüzeyi yukarıdaki Sınırlı ek satırlarıdır.

## Out of scope

- Doküman, down betiği ve denetim istisnası V1-RMD-724'te yapıldı.
- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Void ve ikram gerekçesinin (`order_status_history.reason`) KVKK temizliği: kod görevi olarak 725 başlarken değerlendirilir; 724 yalnız runbook'a açık olarak yazdı.

## Dependencies

- V1-RMD-724

## Acceptance evidence

1. Her gerçek kalem için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-725/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Gerçek olmayan kalemler `evidence/V1-RMD-725/README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-603
