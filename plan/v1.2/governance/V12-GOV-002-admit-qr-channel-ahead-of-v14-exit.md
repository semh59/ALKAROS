# V12-GOV-002 - Admit the QR channel ahead of GATE-V14-EXIT

- Task ID: V12-GOV-002
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

`qr-ordering`/`qr-security`/`qr-transport` görevlerinin, kendileriyle
hiçbir teknik bağı olmayan `GATE-V14-EXIT` (V1.4 cari hesap/dönemsel
faturalama/QNB e-fatura entegrasyonu — 25 görevin tamamı hâlâ `Planned`)
kapanmasını beklemeden ilerleyebilmesini, Semih'in açık iş kararıyla
kaydetmek — tıpkı `V12-GOV-001`'in NFC için yaptığı gibi.

## Owned surface

- `plan/v1.2/governance/V12-GOV-002-admit-qr-channel-ahead-of-v14-exit.md`
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - plan/v1.2/README.md (V12-GOV-001 sahipliğinde) — yalnız bu istisnanın
    kaydı eklendi.
  - plan/v1.2/qr-security/V12-QRS-001-qr-token-lifecycle.md (PDF kaynaklı,
    henüz `Unassigned`/`Planned`) — `Dependencies`'ten yalnız
    `GATE-V12-ENTRY` satırı çıkarıldı; `V0-QRG-001` (artık `Done`) korundu.
    Bu, transitive zincir üzerinden `V12-QRS-002`/`V12-QRS-003`/
    `V12-QRO-001/002/003`/`V12-CWB-001/002`'nin de aynı istisnadan
    yararlanmasını sağlıyor — hiçbiri `GATE-V12-ENTRY`'yi doğrudan
    listelemiyordu, yalnız `V12-QRS-001` üzerinden miras alıyorlardı.

## In scope

- Yalnız `qr-ordering`, `qr-security`, `qr-transport` ve bunlara bağımlı
  `customer-web`'in QR'a özgü görevleri (`V12-CWB-001/002`).
- `plan/v1.2/README.md`'nin "Giriş koşulu" bölümünün bu istisnayı da
  NFC'yle aynı biçimde kaydetmesi.

## Out of scope

- `online-ordering`, `channel-mapping`, `reconciliation`, `reporting`,
  `shared-stock` — bunlar hâlâ `V0-YSP-001` (Yemeksepeti partner API,
  hâlâ `Blocked`) bağımlılığını taşıyor, bu görev onu değiştirmez.
- `GATE-V14-EXIT`in kendisini kapatmak veya V1.4'ün kapsamını değiştirmek.

## Dependencies

- V0-QRG-001

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-07 ("Ben öne çektim
kim karışır buna, sıkıntı ne"). QR'ın cari hesap/faturalama/QNB
entegrasyonuyla hiçbir teknik bağı yok; V1.4 ile aynı "V1.2" şemsiyesi
altında sırf sürüm numarası komşuluğu yüzünden birbirini beklemesi
gerekmiyor. Not: aynı gün yapılan sürüm yeniden numaralandırması
(`V12-GOV-003`) QR'ı kalıcı olarak `V1.2` yaptığı için bu görevin çözdüğü
istisna artık yapısal olarak gereksizdir; görev geçmiş kayıt olarak
`Done` kalır.

## Deliverables

- `plan/v1.2/README.md`'de kayıtlı istisna.
- `V12-QRS-001`'in `Dependencies`'inden `GATE-V12-ENTRY`'nin çıkarılması.

## Acceptance evidence

- `plan/v1.2/README.md`, QR modüllerinin `GATE-V14-EXIT`'ten bağımsız
  olarak ilerleyebileceğini adıyla ve gerekçesiyle yazar.
- `V12-QRS-001` artık yalnız `V0-QRG-001`'e (Done) bağımlı; transitive
  zincirdeki hiçbir QR/customer-web görevi artık `GATE-V12-ENTRY`
  üzerinden `DONE_DEPENDENCY_TRANSITIVE_NOT_FINAL` ile bloklanmıyor
  (`python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata).
- `online-ordering`/`channel-mapping`/`V0-YSP-001` zinciri değişmedi.

## Handoff

- V12-QRS-001
