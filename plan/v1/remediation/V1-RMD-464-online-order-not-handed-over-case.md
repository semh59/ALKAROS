# V1-RMD-464 - Teslim edilmeden açık kalan online siparişleri vaka olarak göstermek

- Task ID: V1-RMD-464
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

Online sipariş kabul edilir ama personel "Kuryeye teslim et"e basmazsa sipariş `Accepted`/`Preparing`/`Ready` durumunda
sonsuza kadar açık kalır: stok tutmaları serbest kalmaz, tüketim yazılmaz ve kimse uyarılmaz. Bu görev, 3 saatten uzun
süredir açık online siparişi "Sorunlar" sekmesinde bir uzlaştırma vakası olarak gösterir. Sipariş sistem tarafından
kendiliğinden kapatılmaz (gerçek durumu yalnız restoran bilir); teslim edilince ya da iptal edilince vaka yeniden
kontrolde kendiliğinden çözülür.

## Owned surface

- `plan/v1/remediation/V1-RMD-464-online-order-not-handed-over-case.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reconciliation/OnlineOrders/NotHandedOverSourcePair.cs - yeni kaynak çifti
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reconciliation/OnlineOrders/OnlineOrderReconciliationModels.cs - yalnız yeni ayrışma türü ve önerilen eylem
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reconciliation/OnlineOrders/OnlineOrderReconciliationModule.cs - yalnız yeni kaynak çiftinin kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reconciliation/OnlineOrders/NotHandedOverSourcePairTests.cs - yeni test dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reconciliation/OnlineOrderReconciliationHttpTests.cs - yalnız tarama sonucundaki kaynak çifti sayısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-problems/onlineProblemsApi.ts - yalnız yeni tür ve eylem için Türkçe etiketler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-problems/OnlineProblemsTab.test.tsx - yalnız yeni etiketlerin testi

## In scope

- `orders.orders.source = 'Online'` ve durumu `Accepted`, `Preparing` ya da `Ready` olup 3 saatten eski siparişler için
  `NotHandedOver` vakası; önerilen eylem `HandOverOrCancelOrder` ("Siparişi teslim edin ya da iptal edin"), yeniden deneme
  yok (`canRetry` false), yeniden kontrolde kaynaktan düşen vaka çözülür.
- Vaka atlanan (Dismissed) sipariş için yeniden açılmaz.
- Testler: eşik altı sipariş vaka üretmez, eşik üstü üretir, teslim edilen çözülür, sağlayıcı ayrımı korunur.

## Out of scope

- Siparişi otomatik kapatmak ya da stok tutmasını otomatik serbest bırakmak; eşik ayarı arayüzü.

## Dependencies

- None

## Acceptance evidence

- Testler ve gerçek Host denemesi (eski açık online sipariş `V1-RMD-463`ün zamanlanmış taramasıyla ya da elle taramayla
  Sorunlar listesinde Türkçe metinle görünür); çıktılar `evidence/V1-RMD-464/` altındadır.

## Handoff

- None
