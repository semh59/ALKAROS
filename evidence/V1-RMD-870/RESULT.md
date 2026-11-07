# V1-RMD-870 sonuç

Aynı ödemeye eşzamanlı iade talebi sınanmıyordu. Yeni test: hesap kilidi tutulurken 6 talep (farklı işlem anahtarları, açık kasa oturumu yok) kilitte birikir, kilit bırakılınca birlikte çalışır; tam biri 200, beşi 409 REFUND_ALREADY_REQUESTED, payments.refund_requests'te tek Requested satır (100), iade kaydı (reversal) yok. Üretim kodu değişmedi.
Testler: Host Billing PaymentRefundHttpTests 77/77 (tam sınıf, ilk sürüm) ve yeni test 1/1 (g-1.log).
Mutasyon (cp+cmp geri alındı): TryInsertAsync çakışmasını yok saymak öldü (mut-1.log).
Kilit mutantı (hesap kilidi kaldırılırsa) ilk turda sağ kaldı ve bu özellik için ayırt edilemez: ödeme satırı FOR UPDATE ve ödeme başına tek açık istek kısmi tekil indeksi (uq_refund_requests_open_per_payment, migration 183) ikinci/üçüncü savunma. Hesap kilidi ödeme/iptalle serileştirme için (ayrı testli); bu testin konusu değil.
