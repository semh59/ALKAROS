# V1-RMD-442 - "Hesaba yaz" ödeme yolu ve kasiyerin cari hesap API'si

- Task ID: V1-RMD-442
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

Semih'in kararı (2026-09-29, "nakitle tam özellik"): kasiyer bir adisyonu müşterinin cari hesabına yazabilsin, müşteri
borcunu kasaya nakit ödeyebilsin. Planlı V14-ACC-008 (hesaba yaz yönlendirmesi) mali belge kararını (V13-FSC-001,
Blocked) beklediği için bu görev hesaba yazmayı bugünkü nakit ve kart adisyon tahsilatlarıyla aynı yoldan bağlar:
onaylanan tahsilat adisyonu mevcut kapanış servisiyle kapatır, mali belge üretilmez.

Bu görev:

- `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/account-charge` (`payments.take`): V14-ACC-003
  `AccountChargeHandler` (V1-RMD-440 kredi limiti ve vade kontrolüyle); onaylanınca adisyon kapanışı denenir.
- `AccountChargeHandler` diğer ödeme yollarıyla aynı güvenceleri alır: önce `bill-settlement` kilidi (V1-RMD-409),
  adisyonda çözülmemiş kart denemesi varken ret, işlem kimliği başka adisyon/tutar için kullanılmışsa ret (V1-RMD-415
  nakit için yaptığının aynısı).
- Kasiyerin müşteri uçları (`payments.take`): liste ve arama (ad, maskelenmiş telefon, bakiye, limit, kullanılabilir
  kredi, vade), müşteri ekleme (ad zorunlu, telefon isteğe bağlı), ekstre (cari hareketler ve makbuzlar).
- `POST /api/v1/terminals/{terminalId}/cash-sessions/{cashSessionId}/account-receipts` (`cash.drawer` +
  `payments.take`, oturum bu terminalin olmalı): V14-ACC-005 nakit cari tahsilat ve V14-ACC-009 makbuzu; yanıt
  makbuz numarası ve kalan bakiyeyi döner.
- Hata yanıtları Türkçe (kredi reddinde politikanın gerekçesi aynen gösterilir).

## Owned surface

- `plan/v1/remediation/V1-RMD-442-account-charge-tender-and-customer-account-api.md`
- `evidence/V1-RMD-442/**`
- `src/Host/Experience/CustomerAccounts/**`
- `tests/Host/Experience/CustomerAccounts/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs (ana Host kompozisyonu) —
  yalnız yeni uçların eşlenmesi ve servis kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/BillCharges/AccountChargeHandler.cs,
  src/Modules/CustomerAccounts/BillCharges/AccountChargeExceptions.cs ve
  tests/Modules/CustomerAccounts/BillCharges/AccountChargeHandlerTests.cs (V14-ACC-003 sahipliğinde) — yalnız
  adisyon kilidi, çözülmemiş ödeme ve işlem kimliği güvenceleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx (V0-GOV-040 sahipliğinde) — yalnız yeni test projesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-272
  sahipliğinde) — yalnız artık erişilebilir olan tiplerin satırlarının kaldırılması

## In scope

- Hesaba yaz ucu, kasiyerin müşteri/ekstre/nakit tahsilat uçları ve hesaba yazma güvenceleri.

## Out of scope

- Kartla cari tahsilat (V14-ACC-006, Token/Beko cihazı), tahsilat mutabakatı (V14-ACC-007) ve mali belge.
- Müşteri düzenleme ve anonimleştirme uçları.
- Arayüz (ayrı görev).

## Dependencies

- V14-ACC-005
- V14-ACC-009
- V1-RMD-440

## Acceptance evidence

- HTTP testleri (gerçek PostgreSQL 18): limitli müşteriye hesaba yazma adisyonu kapatır ve bakiyeyi artırır; limitsiz ya
  da limiti aşan müşteride Türkçe gerekçeli 409 ve hiçbir kayıt yok; yetkisiz 403; nakit tahsilat makbuz numarası döner
  ve bakiyeyi düşürür; başka terminalin kasa oturumu 404; ekstre hareketleri ve makbuzları gösterir; liste telefonu
  maskeler. `evidence/V1-RMD-442/tests.log`.
- Modül testleri: aynı işlem kimliği başka adisyon ya da başka tutar için kullanılırsa ret; çözülmemiş kart denemesi
  varken ret, hiçbir kayıt yok. Güvenceler geri alınınca kırmızı (`evidence/V1-RMD-442/red-without-fix.log`).
- Semih'in elle deneyebileceği senaryo: arayüz görevi tamamlanınca kasiyer ekranından.

## Handoff

- None
