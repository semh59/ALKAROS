# V1.5 - Operational Hardening

## Hedef

Güvenlik, gözlemlenebilirlik, backup/restore, KVKK operasyonu, performans ve tam
mutabakat olgunluğunu sağlamak.

## Giriş koşulu

V1.4 çıkış kapısı kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 18 görev dosyasının tamamı `Done`.
- Kritik failure injection senaryoları veri kaybı oluşturmadan geçer.
- Restore otomasyonu temiz ortam testine hazırdır; ölçülmüş nihai RPO/RTO kabulü
  V20-DRL-001'e aittir.
- Güvenlik ve KVKK açıkları kapatılmıştır.
- Operasyon runbook'ları başka bir operatör tarafından uygulanmıştır.

## Modüller

`backup-recovery`, `kvkk`, `notifications`, `observability`, `performance`,
`reconciliation`, `reporting`, `runbooks`, `security`, `support`.

Doğrulanan plan hacmi: 10 modül, 18 tek-sahip görev.
