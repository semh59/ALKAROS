# V1-RMD-473 - Yönetim: online sipariş faturaları ekranı

- Task ID: V1-RMD-473
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

Açılan fatura taslaklarını görmek için liste (`GET /api/v1/management/order-invoices?from&to`, hizmet tarihine göre) ve sipariş başına ayrıntı (`GET .../by-order/{orderId}`) uç noktası ile Yönetim alanında "Online faturalar" bölümü: sipariş, tarih,
platform, tutar (brüt, KDV), durum (Türkçe: Taslak), faturasız (taslağı açılamamış)
siparişlerin listesi ve yasal süreye kalan gün. "Alıcı bilgisi eksik" uyarısı eşik tutarı doğrulanmadığı için yoktur. Yalnız okur; gönderim yoktur (`V0-QNB-001` engelli).

## Owned surface

- `plan/v1/remediation/V1-RMD-473-online-order-invoice-list-screen.md`
- `src/Host/Experience/OrderInvoices/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reconciliation/OrderInvoiceHttpTests.cs - yeni dosya: uç nokta testleri (yönetici oturumu ve gerçek modül bileşimi burada hazır)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reconciliation/ALKAROS.Host.Experience.Reconciliation.Tests.csproj - yalnız fatura şeması ve migration 171 fixture satırları
- `src/Clients/PosTerminal/src/features/management-order-invoices/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/sections.ts - yalnız yeni bölümün kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız yeni bölümün Türkçe metinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ManagementArea.test.tsx - yalnız bölüm listesi beklentisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız yeni uç noktaların ve servislerin kaydı

## In scope

- `GET` liste (tarih aralığı, en çok 31 gün) ve ayrıntı; yetki `reports.view` (kanal raporu uç noktasıyla aynı yönetim oturumu); ekran durumları; İngilizce sözcük ekrana çıkmaz.
- Testler: uç nokta (yetki, aralık), istemci (tablo, uyarı, axe).

## Out of scope

- Taslak üretimi (`V1-RMD-470`, `V1-RMD-472`); gönderim, iptal, iade.

## Dependencies

- V1-RMD-470

## Acceptance evidence

- Testler ve gerçek Host denemesi (teslim edilmiş online siparişin taslağı listede görünür); çıktılar `evidence/V1-RMD-473/` altındadır.

## Handoff

- None
