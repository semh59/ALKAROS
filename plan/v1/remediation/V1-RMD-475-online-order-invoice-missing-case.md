# V1-RMD-475 - Faturasız kalan online sipariş için uzlaştırma vakası

- Task ID: V1-RMD-475
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Restoran, platform kullanıcısına teslimden sonra en geç 7 gün içinde e-Arşiv fatura düzenlemek zorundadır. Teslim edilmiş ama taslağı açılamamış (satıcı bilgisi eksik, hata) sipariş
süre dolmadan önce Sorunlar listesinde vaka olarak görünür: Türkçe etiket, sonraki adım ("İşletme bilgilerini girin" ya da "Faturayı elle düzenleyin"). Kaynak çifti
`NotHandedOverSourcePair` kalıbıyla yazılır; taslak açılınca vaka kendiliğinden kapanır. Yönetim ekranındaki faturasız sipariş listesi ayrıdır ve vakayı beklemez.

## Owned surface

- `plan/v1/remediation/V1-RMD-475-online-order-invoice-missing-case.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reconciliation/OnlineOrders/MissingInvoiceSourcePair.cs - yeni kaynak çifti
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reconciliation/OnlineOrders/OnlineOrderReconciliationModels.cs - yalnız yeni ayrışma türü ve önerilen eylemler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reconciliation/OnlineOrders/OnlineOrderReconciliationModule.cs - yalnız yeni kaynak çiftinin kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reconciliation/OnlineOrders/MissingInvoiceSourcePairTests.cs - yeni test dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reconciliation/OnlineOrderReconciliationHttpTests.cs - yalnız tarama sonucundaki kaynak çifti sayısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-problems/onlineProblemsApi.ts - yalnız yeni tür ve eylemler için Türkçe etiketler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-problems/OnlineProblemsTab.test.tsx - yalnız yeni etiketlerin testi

## In scope

- Servis edilmiş ya da tamamlanmış, son 7 günde kapanmış, 1 saatten eski ve taslağı olmayan online sipariş için `MissingInvoice` vakası; satıcı bilgisi yoksa eylem `EnterSellerProfile`, varsa `IssueInvoiceManually`; yeniden deneme yok; atlanan vaka yeniden açılmaz.

## Out of scope

- Taslak açma (`V1-RMD-472`).

## Dependencies

- V1-RMD-472

## Acceptance evidence

- Testler ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-475/` altındadır.

## Handoff

- None
