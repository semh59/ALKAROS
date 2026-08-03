# GATE-V0-EXIT Durum Kaydı

- Tarih: 2026-08-03
- Gate: `GATE-V0-EXIT`
- Durum: **Open**
- Kaynak: plan metadata tam okuma ve `plan_audit_tool.py validate`.

## Güncel mekanik sayım

Bu kayıt kapanışta V0 altında 62 task bulunur: `15 Done`, `47 Blocked` ve
`0 InProgress`. Açık 47 task nedeniyle `GATE-V0-EXIT` kapanamaz.

## Açık Blocked tasklar

```text
V0-ARC-001 V0-ARC-002 V0-ARC-003 V0-ARC-004 V0-ARC-005 V0-ARC-006
V0-ARC-007 V0-ARC-008 V0-ARC-009 V0-BKP-001 V0-BKP-002 V0-CMP-001
V0-CMP-002 V0-CMP-003 V0-CMP-004 V0-DAT-001 V0-DAT-002 V0-DAT-003
V0-DAT-004 V0-DAT-005 V0-DAT-006 V0-DOC-001 V0-DOM-001 V0-DOM-002
V0-DOM-003 V0-DOM-004 V0-DOM-005 V0-DOM-006 V0-DOM-007 V0-DOM-008
V0-DOM-009 V0-DOM-010 V0-GOV-010 V0-GOV-011 V0-GOV-012 V0-GOV-013
V0-GOV-014 V0-GOV-015 V0-GOV-016 V0-HUG-001 V0-LIC-001 V0-MCD-001
V0-PRN-001 V0-QNB-001 V0-QRG-001 V0-SEC-001 V0-YSP-001
```

Her blocker'in kaynak, kaldırma koşulu ve etkilediği tasklar kendi task
dosyasında kayıtlıdır. Bu kayıt herhangi bir `Blocked` task'ı terminal saymaz.

## Tarihsel hata kaydı

2026-08-02 tarihli önceki kayıt `54 task`, `33 Done`, `21 Blocked` sayımını
gösteriyordu. Bu sayı, transitive dependency kapanışı uygulanmadan önceki
tarihsel hatadır; silinmemiş, bu kayıtla geçersiz kılınmıştır. Daha eski
`34 Done / 8 Blocked` sayımı da geçersizdir.

## Karar

V0 karar, uyum, güvenlik, recovery ve dış sözleşme blocker'ları gerçek kanıtla
kapanmadan `GATE-V1-ENTRY` açılmaz. Geçmiş Git/application ağacı yalnız
candidate evidence'dır; yeniden doğrulanmadan `Done` kabul edilmez.
