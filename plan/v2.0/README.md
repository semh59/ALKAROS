# V2.0 - Production Acceptance

## Hedef

Modular Monolith ürünü kontrollü, geri alınabilir ve kanıtlı biçimde production'a
hazırlamak.

## Giriş koşulu

`GATE-V20-ENTRY` kapanmış olmalıdır.

## Çıkış kapısı

- Licensing dışındaki 25 sabit görev, iki licensing task'inin kanıtlı terminal
  sonucu ve her approved meal-card provider için bir `V20-INT-1xx` görevi kapanmış olmalıdır.
- Tüm release gate görevleri `Done`; koşullu task yalnız kanıtlı `NotApplicable` olabilir.
- Migration, rollback, restore, security ve compliance onayları mevcut.
- `INT-001`/`INT-002` görevleri ya gerçek sandbox/cihaz kanıtıyla `Done` ya da
  `GATE-V12-FSC-STRATEGY` kararına dayanan tarihli `NotApplicable` olarak kapanır.
- Yemeksepeti, meal-card, printer ve QR public path certification görevleri gerçek
  sandbox/cihaz/ağ kanıtıyla `Done`.
- Non-production pilot rehearsal ölçümleri kabul sınırları içinde.
- Açık kritik veya yüksek önemde defect yok.

## Modüller

`acceptance`, `data-migration`, `documentation`, `installer-updates`,
`integration-certification`, `licensing`, `recovery-drill`, `release`,
`release-gates`, `security-compliance`.

Doğrulanan plan hacmi: 10 modül, `27 + approved meal-card provider count`
tek-sahip görev. Licensing task dosyaları `NotApplicable` sonucunda silinmez.
