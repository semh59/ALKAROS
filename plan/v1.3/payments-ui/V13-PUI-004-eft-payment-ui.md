# V13-PUI-004 - EFT/Havale ödeme ekranı

- Task ID: V13-PUI-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-16

## Goal

V13-PUI-001'in ödeme ekranına (Nakit/Kredi Kartı/Yemek Kartı) dördüncü bir
yöntem kartı eklemek: EFT/Havale. Tasarım taslağı zaten review edildi
(kasa-onizleme artifact, 2026-09-16): kasiyer EFT'yi seçtiğinde bir onay
kutusu çıkar ("Tutarı işletmenin banka hesap hareketinde gördüm"), bu
işaretlenmeden ödeme uygulanmaz. Referans numarası alanı YOK (Semih'in
tercihi).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/Payments/SplitPayment/**
  (V13-PUI-001 sahipliğinde kalır, bu görev V13-PUI-001'den SONRA çalışır)
  — yalnız dördüncü tender kartı ve onay kutusu eklenir; Cash/BankCard/
  MealCard akışları değişmez.
- `evidence/V13-PUI-004/**`

## In scope

- Dördüncü tender kartı: "EFT/Havale".
- Seçildiğinde görünen onay kutusu; işaretlenmeden "Ödemeyi Ekle" pasif
  kalır (foundations.md'nin disabled-buton kuralına uygun).
- Para üstü gösterimi YOK (V13-PAY-005'in kuralıyla tutarlı — asla aşamaz).
- Vardiya kapalıyken de bu kart aktif kalır (Nakit'in aksine, bkz.
  V13-PAY-005 In scope) — V1-WTR-055/V1-CUI-010'un vardiya-kapalıyken-
  nakit-kapanır davranışıyla KARIŞTIRILMAZ.

## Out of scope

- Referans numarası veya dekont fotoğrafı yükleme.
- Banka API entegrasyonu.

## Dependencies

- V13-PAY-005
- V13-PUI-001

## Acceptance evidence

- Gerçek tarayıcıda: EFT seçilip onay kutusu işaretlenmeden "Ödemeyi Ekle"
  tıklanamaz; işaretlenince tutar kadar (veya kalanı aşmayan bir kısmı)
  uygulanır, para üstü gösterilmez.
- Semih'in elle deneyebileceği senaryo: bir hesabı EFT ile tam kapat,
  hesabın "Ödendi" durumuna geçtiğini doğrula.
