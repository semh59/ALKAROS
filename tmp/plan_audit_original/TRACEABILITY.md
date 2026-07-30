# Audit Traceability

Bu tablo, master PDF incelemesinde doğrulanan önemli boşlukların hangi görevde
kapatılacağını gösterir. Tam bölüm kapsamı `PDF_COVERAGE.md`, kaynak dosya kimliği
`PDF_SOURCE.md` içindedir.

| Audit finding | Decision/contract owner | Implementation/verification owner |
|---|---|---|
| State machine geçişleri yalnızca isim listesi | V0-DOM-001 | İlgili lifecycle task'ları, V20-GAT-001 |
| Bill birden fazla Order'ı temsil edemiyor | V0-DOM-002 | V1-BIL-001, V1-BIL-002 |
| Kısmi iade ve allocation ters kaydı eksik | V0-DOM-003 | V12-PAY-002, V12-ALC-003 |
| PaymentAllocation çapraz-bill bütünlüğü ve idempotency scope'u eksik | V0-DOM-004 | V12-ALC-001, V1-FND-002 |
| Migration sırası ve döngüsel/forward FK'ler | V0-DAT-001 | V20-MIG-001, V20-MIG-002 |
| Kanonik status/enum/discriminator listeleri eksik | V0-DAT-002 | Tüm migration sahipleri, V20-GAT-001 |
| Nullable unique kısıtları `NULL` tekrarını engellemiyor | V0-DAT-003 | İlgili migration sahipleri |
| Cached projection sahipliği/rebuild yolu belirsiz | V0-DAT-004 | V1-TBL-005, V11-INV-002, V15-REC-001 |
| Local-first senkronizasyon sözleşmesi yok | V0-ARC-002 | V1-WTR-001, V15-PER-002 |
| Genel inbox/outbox/idempotency modeli yok | V0-ARC-003 | V1-FND-002, V14-ONL-001 |
| Güncel GİB/e-Adisyon kapsamı PDF'de doğrulanmamış | V0-CMP-001 | V12-FSC-003, V20-CMP-001 |
| KVKK veri envanteri dar ve lifecycle uygulaması eksik | V0-CMP-003 | V15-KVK-001, V15-KVK-002 |
| Table reservation ve QR PendingConfirmation seating race | V0-DOM-005 | V1-TBL-004, V14-QRO-002 |
| Discount/complimentary/fee/tip davranışı tamamlanmamış | V0-DOM-006, V0-CMP-004 | V1-ORD-003, V1-BIL-003 |
| Customer account sign/invoice reclassification semantiği belirsiz | V0-DOM-007 | V13-ACC-001, V13-ACC-003 |
| Report formülleri isim olarak var, ölçüm contract'ı yok | V0-DOM-008 | V1/V11/V12/V13/V14/V15 reporting task'ları |
| Satın alma ve supplier account etkisi eksik | V0-DOM-007 | V11-PUR-001, V11-PUR-002, V13-PUR-001 |
| Stokta birden fazla source-of-truth riski var | V0-DAT-004 | V11-INV-001, V11-INV-002 |
| Meal-card settlement parent/child status drift riski | V0-DAT-004 | V12-MCD-002 |
| QR token/session ve masa durumu saldırı yüzeyi | V0-QRG-001, V0-CMP-003 | V14-QRS-001, V14-QRS-002, V14-QRS-003, V14-QRO-002 |
| Online catalog/availability outbound davranışı eksik | V0-YSP-001 | V14-ONL-004, V14-ONL-005 |
| Printer retry fiziksel exactly-once sağlamıyor | V0-PRN-001 | V1-KIT-004, V20-INT-005 |
| RPO/RTO başlıkları ölçülebilir karar içermiyor | V0-BKP-002 | V20-DRL-001 |
| Licensing şeması çalışma/offline failure contract'ını vermiyor | V0-LIC-001 | V20-LIC-001, V20-LIC-002 |
| PDF'deki C8 düzeltmesi I.46'ya uygulanmamış | V0-DOC-001 | V20-GAT-001 |
