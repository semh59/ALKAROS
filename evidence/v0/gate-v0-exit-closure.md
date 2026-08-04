# GATE-V0-EXIT Durum Kaydı

- Tarih: 2026-08-03
- Gate: `GATE-V0-EXIT`
- Durum: **Open — kapanış koşulu sağlandı; kapanış kararı GATE-V0-EXIT kullanıcı onayına tabidir**
- Kaynak: plan metadata tam okuma ve `plan_audit_tool.py validate` / `validate-coverage` / `verify-manifest`.

## Güncel mekanik sayım

Bu kayıt kapanışta V0 altında 62 task bulunur: `51 Done`, `11 Blocked` ve
`0 InProgress`. Kalan 11 Blocked görev, 2026-08-03 kullanıcı onaylı devir
listesindeki (`GATES.md` "user-approved V0 deferrals", `TRACEABILITY.md`
C40) görevlerdir ve `GATE-V0-EXIT` kapanma koşulundan muaftır.

## Kalan Blocked tasklar (kullanıcı onaylı devir)

```text
V0-BKP-001 V0-BKP-002 V0-CMP-001 V0-HUG-001 V0-LIC-001 V0-MCD-001
V0-PRN-001 V0-QNB-001 V0-QRG-001 V0-SEC-001 V0-YSP-001
```

Her devredilen görevin kanıt koşulu ve kapanış aşaması `GATES.md` devir
tablosunda ve kendi task dosyasındaki `Blocker` bölümünde kayıtlıdır. Bu
kayıt, devredilen görevler için V0 kapanış kanıtı üretmez; kanıtları ilgili
aşamada (V12-V20) toplanır.

## Kapanış işlemleri

- 2026-08-03: 21 karar/validation görevi (`V0-ARC-002/003/005/006/007/008/009`,
  `V0-CMP-002/003/004`, `V0-DAT-001/003/004/005/006`,
  `V0-DOM-005/006/007/008/009/010`) karar kayıtları ve gerçek kanıtlarla
  `Done` oldu.
- 2026-08-03: `V0-GOV-010..016` gerçek test kanıtıyla (`py -m pytest
  tests/Architecture/TaskScope -q` → 73 passed; `dotnet test ALKAROS.slnx`
  → 258 passed) `Done` oldu.
- 2026-08-03 kullanıcı onaylı plan değişikliği `C39` ile V1-FND zincirine
  bağımlı GOV dep'leri ve `V0-SEC-001`/`V0-CMP-001` kaldırıldı (araç
  `DEPENDENCY_REMOVALS` + forbidden seti; kayıt `TRACEABILITY.md` C39).
- 2026-08-03 kullanıcı onaylı plan değişikliği `C40` ile 11 dış-girdi görevi
  devredildi (kayıt `TRACEABILITY.md` C40).

## Doğrulama

```text
plan_audit_tool.py validate          Exit code: 0, Validation errors: 0
plan_audit_tool.py validate-coverage Exit code: 0, Coverage errors: 0
plan_audit_tool.py verify-manifest   Exit code: 0
```

## Tarihsel hata kaydı

2026-08-03 önceki kayıt `62 task, 15 Done, 47 Blocked` sayımını gösteriyordu
(69ae032 revert sonrası). 2026-08-02 tarihli `54 task, 33 Done, 21 Blocked`
sayımı transitive dependency kapanışı uygulanmadan önceki tarihsel hatadır.
Her iki sayım silinmemiş, bu kayıtla geçersiz kılınmıştır.

## Karar

Devredilen 11 görev hariç tüm V0 karar, uyum, güvenlik, recovery ve dış
sözleşme görevleri gerçek kanıtla `Done` durumundadır. Kapanış kararı
(plan metadata'da `GATE-V0-EXIT` kapanışının resmen ilanı) kullanıcı
onayına tabidir; devir yeni product behavior başlatma izni vermez.
