# V1-RMD-292 - Kasada indirim, bahşiş ve düzeltme arayüzü

- Task ID: V1-RMD-292
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

Sunucuda `billing/bills/{id}/discount`, `/tip`, `/adjustments` ve `bills/custom` ile bölünmüş hesap için `split-design/**/discount|tip|adjustments|custom` uç noktaları var (`BillingSplitApplication.cs`); hiçbir istemci bunları çağırmıyor. Gönüllü bahşiş yasal olarak izin verilen tek hizmet bedeli biçimidir; arayüz olmadan kasiyer bahşiş girip indirim uygulayamıyor. Bu görev Kasa ödeme ekranına indirim, bahşiş ve düzeltme akışlarını (yetki ve Türkçe hata çevirisiyle) ekler.

## Owned surface

- `plan/v1/remediation/V1-RMD-292-cashier-discount-tip-adjustment-ui.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/15-discount-and-tip.spec.js

## In scope

1. İndirim, gönüllü bahşiş ve düzeltme formları, yetki kontrolü, Türkçe hata iletileri, testler.

## Out of scope

- Zorunlu hizmet bedeli (yasa dışı, eklenmez).
- Özel hesap (`bills/custom`) oluşturma ekranı ayrı karardır.

## Dependencies

- V13-PUI-001
- V1-RMD-283

## Acceptance evidence

- Cashier E2E (gerçek Host + Chromium): indirim ve bahşiş uygulanır, ödenecek tutar buna göre değişir, yetkisiz kullanıcı reddedilir.
- `plan_audit_tool.py validate` temiz.

## Handoff

- None
