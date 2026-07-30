# V2.0 - Production Acceptance

## Hedef

Modular Monolith ürünü kontrollü, geri alınabilir ve kanıtlı biçimde production'a
hazırlamak.

## Giriş koşulu

V1.5 çıkış kapısı kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 25 görev dosyasının tamamı `Done`.
- Tüm release gate görevleri `Done`.
- Migration, rollback, restore, security ve compliance onayları mevcut.
- Hugin, QNB, Yemeksepeti, meal-card, printer ve QR public path certification
  görevleri gerçek sandbox/cihaz/ağ kanıtıyla `Done`.
- Pilot rollout ölçümleri kabul sınırları içinde.
- Açık kritik veya yüksek önemde defect yok.

## Modüller

`acceptance`, `data-migration`, `documentation`, `installer-updates`,
`integration-certification`, `licensing`, `recovery-drill`, `release`,
`release-gates`, `security-compliance`.

Doğrulanan plan hacmi: 10 modül, 25 tek-sahip görev.
