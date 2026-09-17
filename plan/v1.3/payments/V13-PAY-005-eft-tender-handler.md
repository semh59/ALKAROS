# V13-PAY-005 - EFT/Havale tender handler'ı uygula

- Task ID: V13-PAY-005
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-16

## Goal

Cash/BankCard/MealCard'ın yanına dördüncü bir tender yöntemi eklemek: EFT/
Havale. Provider entegrasyonu YOK — kasiyer tutarı işletmenin banka hesap
hareketinde gözle görüp beyan eder, sistem yalnız bu beyanı kaydeder. Bu
yöntem PDF baseline'da yoktu; Semih'in doğrudan ürün kararıdır
(kasa-onizleme artifact review, 2026-09-16).

## Owned surface

- `src/Modules/Payments/EftTender/**`, `tests/Modules/Payments/EftTender/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- `Eft` tender tipi: tutar, isteğe bağlı serbest metin not alanı (referans
  numarası ZORUNLU DEĞİL — Semih'in tercihi), kasiyer kimliği, zaman damgası.
- V13-PAY-002'nin typed tender request/handler contract'ına yeni bir case
  olarak eklenir (Cash/BankCard/MealCard ile aynı desende).
- V13-PAY-003'ün fail-closed registry'sine kayıt.
- Para üstü YOK — tutar her zaman tam kalan miktar kadar veya daha az
  uygulanır, asla aşamaz (Cash'in aksine).
- Kasa nakit çekmecesiyle/`CashSession`'la HİÇBİR ilgisi yok — vardiya
  açık olmasa da kullanılabilir (bu, Nakit'in aksine; Nakit
  `RecordTransaction` için oturum `Open` şartına bağlı, EFT değil).

## Out of scope

- Banka API/Open Banking entegrasyonu, otomatik mutabakat, gerçek zamanlı
  hesap hareketi doğrulama — hepsi büyük, ayrı bir workstream; bu görev
  yalnız kasiyer beyanını kaydeder.
- Referans numarası zorunluluğu veya format doğrulaması.
- UI — V13-PUI-004'ün kapsamı.

## Dependencies

- V13-PAY-001
- V13-PAY-002
- V13-PAY-003

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı testler: başarılı EFT tender, tutar kalanı aşan
  EFT denemesinin reddi, vardiya kapalıyken EFT'nin YİNE DE kabul edildiğinin
  doğrulanması (Cash'ten farkı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V13-PUI-004
