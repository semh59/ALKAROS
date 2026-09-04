# V1-RMD-100 - Reservation policy doc reconciliation

- Task ID: V1-RMD-100
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: documentation
- Surface state: Existing

## Goal

`design_handoff_alkaros_v1` A maddesi bir kod hatası değil, doküman gecikmesidir.
Shipped davranış onaylı ürün kararıyla zaten uyumlu: masayı elle yalnız
`pos.cashier.mutate` izni olan (cashier/supervisor/manager) bir kullanıcı,
PosTerminal/Cashier kat planı üzerinden rezerve eder; WaiterPwa'da rezervasyon
aksiyonu hiç yoktur; müşteri ekranı `403` alır. Ancak
`docs/domain/table-reservation-policy.md` hâlâ "personel manuel rezervasyon —
reddedildi" ve `V1-TBL-004` hâlâ "rezervasyon UI beklemede" diyor. Bu görev iki
metni shipped gerçekle ve Semih'in 2026-09-03 kararıyla uzlaştırır. Kod, migration
veya IAM değişikliği yok.

## Owned surface

- `plan/v1/remediation/V1-RMD-100-reserve-action-cashier-role-scope.md`
- `docs/domain/table-reservation-policy.md` (yalnız bu görevin kendi
  2026-09-03 tarihli `## Amendment` bölümü ve ilgili satırlar için —
  `V1-TBL-008`'in 2026-09-04 tarihli ikinci, ayrı `## Amendment` bölümü
  kendi sahipliğinde kalır)
- `plan/v1/table-management/V1-TBL-004-table-reservation-record.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `evidence/V1-RMD-100/**`
- Yüzey devri: `docs/domain/table-reservation-policy.md` custody'si `V0-DOM-005`'ten
  bu göreve geçer (yalnız amendment bölümü + ilgili satırlar); `V0-DOM-005`
  historical `Done` kalır. `V1-TBL-004` yalnız `Out of scope` satırında
  uzlaştırılır; `Done` kalır.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez; kod/test/migration
  yüzeyine dokunmaz.

## In scope

- `table-reservation-policy.md`:
  - "Who creates `Reserved`" satırı: QR order state machine **ve** POS/Cashier
    istemcisinden `pos.cashier.mutate` izinli aktörün elle rezervasyon aksiyonu
    (`POST .../table-management/{terminalId}/reservations`). WaiterPwa ve müşteri
    ekranı rezervasyon oluşturamaz.
  - "Rejected alternatives" listesindeki "Personnel-created manual reservations —
    rejected" maddesi kaldırılır; yerine kısa bir kabul notu.
  - Yeni `## Amendment` bölümü: tarih (2026-09-03), approver (Semih), ne değişti,
    neden (kasiyer telefon/yüz yüze rezervasyon alır — standart POS yeteneği),
    etkilenen task ID'leri (`V1-TBL-004`, `V1-RMD-013`, `V14-QRO-003`).
- `V1-TBL-004` `Out of scope`: "Rezervasyon UI beklemede" → "QR-güdümlü rezervasyon
  politikası (`V14-QRO-003`) beklemede; kasiyer istemcisi manuel rezervasyon UI'si
  V1-RMD-017/028 istemci yüzeylerinde teslim edilmiştir" olarak uzlaştırılır.
- `plan/AUDIT_MANIFEST.json` + `plan/AUDIT_REPORT.md`:
  `python -B tools/plan-audit/plan_audit_tool.py generate-audit-report` ardından
  `generate-manifest` ile mevcut ağaca göre yeniden üretilir. Manifest yeniden
  üretildiği için `GATE-V1-EXIT` bu iş için yeniden açılır; resmi reseal ayrı bir
  governance görevine (`V1-GOV-*`) bırakılır.

## Out of scope

- Herhangi bir kod, test veya migration değişikliği; yürürlükteki davranış doğrudur.
- Yeni `waiter` yetki rolü veya `pos.cashier.mutate` izninin bölünmesi
  (`V1-RMD-097` notundaki ertelenmiş ayrım) — ayrı bir yetkilendirme görevi
  gerektirir; bu görev onu başlatmaz. (Bu ayrım oturumdan sonra `V1-IAM-016..024`
  ile zaten gerçekleşti — bkz. amendment'e eklenen "Superseded note".)
- QR sipariş durum makinesinin rezervasyon yolu (`V14-QRO-002` / `V14-QRO-003`).

## Dependencies

- V0-DOM-005
- V1-TBL-004

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` ve `verify-manifest`:
  sıfır hata.
- Kod/test/migration diff yok (bu görevin kendi değişikliği yalnız `docs/`,
  `plan/`, `evidence/` içinde).
- **Semih onayı (2026-09-04, bu sohbet):** `docs/domain/table-reservation-policy.md`
  içindeki `## Amendment` bölümünü ve "Who creates `Reserved`" satırını
  okudu ve yürürlükteki davranışla (kasiyer rezerve eder; garson WaiterPwa'da
  edemez) örtüştüğünü doğruladı — "Garson rezervasyon yapamaz" (verbatim).
  Amendment'teki izin kodu adının (`pos.cashier.mutate`) migration 049 ile
  `tables.reserve`'e değiştiği ayrıca amendment'e eklenen bir "Superseded
  note (2026-09-04)" ile kaydedildi (kararın kendisi değişmedi, yalnız kod
  adı) — bu görevin kapsamına `V1-TBL-008` sınırlı-ek olarak eklendi.

## Handoff

- V14-QRO-003
