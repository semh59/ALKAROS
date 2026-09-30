# V1-RMD-463 - Online sipariş uzlaştırma taramasını zamanlı çalıştırmak

- Task ID: V1-RMD-463
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

Online sipariş uzlaştırma vakaları (platforma bildirilemeyen durum, ölü giden mesaj, teslimden sonra platform iptali,
sürekli hata veren sipariş çekme) yalnız `POST /management/reconciliation/online-orders/scan` elle çağrılırsa oluşuyor;
bu uç noktayı çağıran ekran ya da zamanlayıcı yok, dolayısıyla "Sorunlar" sekmesi bu vakaları pratikte hiç görmüyor.
Bu görev taramayı arka planda düzenli çalıştırır. Vaka üretme ve tekilleştirme kuralları değişmez (tarama zaten
tekilleştirir); yalnız tetikleme eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-463-online-order-reconciliation-scheduled-scan.md`
- `src/Host/Experience/Reconciliation/OnlineOrderReconciliationHostedService.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reconciliation/OnlineOrderReconciliationHostedServiceTests.cs - yeni test dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Reconciliation/OnlineOrderReconciliationEndpoints.cs - yalnız hizmetin kaydı

## In scope

- Host başlarken kısa bir gecikmeden sonra, sonra her 5 dakikada bir `OnlineOrderReconciliationScanner.ScanAllAsync`
  çağıran `BackgroundService`; bir turun hatası yalnız günlüğe yazılır ve bir sonraki turda yeniden denenir; durdurma
  isteğinde temiz çıkış.
- Testler: tur vaka üretir, tekrar eden tur aynı vakayı çoğaltmaz, kaynak hatası diğer kaynakları ve sonraki turu
  durdurmaz.

## Out of scope

- Yeni vaka türleri (`V1-RMD-464`); ödeme uzlaştırma taramasının zamanlanması; "Şimdi tara" düğmesi.

## Dependencies

- None

## Acceptance evidence

- Testler ve gerçek Host denemesi (eski bir platform iptal olayı kayda konur, elle çağrı yapılmadan Sorunlar listesinde
  vaka belirir); çıktılar `evidence/V1-RMD-463/` altındadır.

## Handoff

- None
