# V1-RMD-265 - Ödeme mutabakat taraması, ödeme raporu, Tahsilat ve Kasa Oturumu sayfaları çalışan uygulamadan erişilebilir olur

- Task ID: V1-RMD-265
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Erişilebilirlik envanteri (Faz 1/Faz 2 `Done` görevleri) şunu gösterdi: kayıtlı ve
testli olmalarına rağmen çalışan uygulamada hiçbir çağıranı olmayan yüzeyler var.
Bu görev Faz 2'ye ait olanları bağlar:

1. `V13-REC-001` `PaymentReconciliationScanner`: çağıranı yoktu. Artık
   `POST /api/v1/management/payments/reconciliation-scan` (yönetici oturumu +
   `reports.view`; tarama vaka yazdığı için `reconciliation.manage`).
2. `V13-RPT-001` `PaymentSettlementReportService`: çağıranı yoktu. Artık
   `GET /api/v1/management/payments/settlement-report?businessDate=` (`reports.view`).
3. `V13-PUI-001` Tahsilat sayfası: hiçbir ekrandan bağlantı yoktu (yalnız elle
   yazılan `?billId=`). PosTerminal "Hesap bölme" ekranında hesap varken
   "Tahsilata geç" bağlantısı eklendi.
4. `V13-PUI-002` Kasa Oturumu sayfası: hiçbir ekrandan bağlantı yoktu. Kasiyer
   ekranının başlığına "Kasa oturumu" bağlantısı eklendi.

Bunların yönetici arayüzü (rapor/tarama için ekran) bu görevde YOK; önceki
ölü-modül bağlama görevlerindeki emsal gibi HTTP yüzeyi açıldı, arayüz ayrı iş.

## Owned surface

- `plan/v1/remediation/V1-RMD-265-payment-surfaces-reachable.md`
- `src/Host/Experience/Reconciliation/PaymentSettlementEndpoints.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reconciliation/PaymentSettlementHttpTests.cs
  (V1-RMD-250 sahipliğindeki test projesine eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/13-payment-surfaces-reachable.spec.js
  (V1-CUI-011 sahipliğindeki Cashier E2E paketine eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (Host sahibi görevlerde kalır — yalnız `MapPaymentSettlementApi()` çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
  (PosTerminal sahibi görevlerde kalır — yalnız BillingRoute'a "Tahsilata geç" bağlantısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/billing.css
  (yalnız bağlantının stili)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html
  (Cashier sahibi görevlerde kalır — yalnız başlıktaki "Kasa oturumu" bağlantısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css
  (yalnız bağlantı olarak kullanılan oturum hapının rengi)

## In scope

1. İki yönetici uç noktası ve gerçek modül bileşimiyle Host HTTP testleri.
2. İki gezinme bağlantısı ve yalnızca bağlantılardan yürüyen gerçek tarayıcı testleri.

## Out of scope

- Rapor/tarama için yönetici arayüzü ekranı.
- Faz 1 (`V15-SEC-001/002/003`, `V15-BKP-001/002`, `V15-SUP-001`, `V15-OBS-001`)
  servislerinin tetikleyicileri (zamanlayıcı/yönetici uç noktası) — ayrı görev(ler).
- Tarama için periyodik zamanlayıcı.

## Dependencies

- V13-REC-001
- V13-RPT-001
- V13-PUI-001
- V13-PUI-002
- V1-RMD-250

## Acceptance evidence

- Host.Experience.Reconciliation (UTF8 Postgres 18): 8/8 (3 yeni: anonim 401 / yetkisiz 403;
  salt-okur yönetici raporu okur, tarama 403; `reconciliation.manage` taramayı koşar, 7 kaynak,
  3'ü kendini "devre dışı" olarak bildirir).
- Cashier E2E: 29/29; yeni spec 13 gerçek `BankCard` tahsilatından sonra taramanın
  `hugin-unknown:{paymentId}` vakasını açtığını, raporun okunduğunu ve iki sayfaya yalnız
  bağlantı tıklanarak gidildiğini sürer.
- PosTerminal `tsc --noEmit` temiz; `workspace` + `billing` vitest 17/17.
- Dürüstçe belirtilen sınır: bağlantı eklerini geri alarak yapılan mutasyon kontrolü koşulmadı
  (öğe yoksa test zaten bulamaz); uç nokta testleri ise gerçek modül bileşimine karşı koşuyor.
- `python tools/plan-audit/plan_audit_tool.py validate`, `python tools/consistency-audit/consistency_audit.py`
  ve `python tools/project-manifest/project_manifest_tool.py` çalıştırıldı.

## Handoff

- None
