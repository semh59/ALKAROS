# GATE-V0-EXIT Durum Kaydi

- Tarih: 2026-08-02
- Gate: `GATE-V0-EXIT`
- Durum: **Open**
- Kaynak: plan metadata tam okuma ve `plan_audit_tool.py validate`.

## Mekanik sayim

Bu kayit uretilirken V0 altinda 51 task bulundu: `30 Done`, `21 Blocked`.
Dolayisiyla `GATE-V0-EXIT` kapanamaz.

## Acik Blocked tasklar

| Task ID | Durum |
| --- | --- |
| V0-ARC-001 | Blocked |
| V0-ARC-004 | Blocked |
| V0-BKP-001 | Blocked |
| V0-BKP-002 | Blocked |
| V0-CMP-001 | Blocked |
| V0-CMP-002 | Blocked |
| V0-CMP-004 | Blocked |
| V0-DAT-002 | Blocked |
| V0-DOC-001 | Blocked |
| V0-DOM-001 | Blocked |
| V0-DOM-002 | Blocked |
| V0-DOM-003 | Blocked |
| V0-DOM-004 | Blocked |
| V0-HUG-001 | Blocked |
| V0-LIC-001 | Blocked |
| V0-MCD-001 | Blocked |
| V0-PRN-001 | Blocked |
| V0-QNB-001 | Blocked |
| V0-QRG-001 | Blocked |
| V0-SEC-001 | Blocked |
| V0-YSP-001 | Blocked |

Her blocker'in kaynak, kaldirma kosulu ve etkiledigi tasklar kendi task
dosyasinda kayitlidir. Bu kayit herhangi bir Blocked task'i terminal saymaz.

## Karar

Eski `34 Done / 8 Blocked` sayimi gecersizdir. Bu kayit kapanis kaniti degil,
acik gate durumunun kanitidir. V0 karar, uyum, guvenlik, recovery ve dis
sozlesme blocker'lari gercek kanitla kapanmadan `GATE-V1-ENTRY` acilmaz.
