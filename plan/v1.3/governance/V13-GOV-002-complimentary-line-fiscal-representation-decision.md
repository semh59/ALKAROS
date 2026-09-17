# V13-GOV-002 - Decide the fiscal representation of complimentary/comp lines

- Task ID: V13-GOV-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-17

## Goal

Bir `Complimentary` (ikram) satırının fiskal çıktıda nasıl temsil edileceğini
resmi bir karara bağlamak: **tam fiyat + %100 indirim**, asla "hiç olmamış"
gibi görünen bir sıfır/görünmez satır DEĞİL. Bu, `V1-RMD-228`'in zaten
uyguladığı düzeltmenin (bkz. o görevin dosyası) geriye dönük, bağlayıcı karar
kaydıdır — `V13-FSC-*` (fiskal doküman üretimi) bu karara göre tasarlanmak
zorundadır.

## Owned surface

- `docs/domain/complimentary-line-fiscal-representation.md`
- `evidence/V13-GOV-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Bir Bill satırının `Complimentary` durumdayken hangi para alanlarının
  (`NetAmount`/`TaxAmount`/`GrossAmount`/`DiscountAmount`) ne taşıması
  gerektiğinin kararı.
- `V13-FSC-*`'in bu kararı fiskal doküman satırlarına nasıl yansıtacağının
  invariant listesi (üretim kodu değil, yalnız sözleşme).

## Out of scope

- Uygulama kodu — zaten `V1-RMD-228` ile Done.
- Fiskal cihaza (Token/Beko) gerçek gönderim formatı — `V13-FSC-*`'in kendi
  kapsamı.
- İndirim/kampanya (`AdjustmentCalculator`, V1-BIL-003 ailesi) mekanizması —
  ayrı, bu karar onu değiştirmez.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-17 (kasa/ödeme
entegrasyonu araştırması sonrası doğrudan talep: "İkram mekanizmasını
düzelt: Complimentary satırını 0 TL yerine gerçek fiyat + %100 indirim
(`adjust`) olarak fiskal katmana çevir — GİB uyumu için şart"). Gerekçe:
Türk fiskal cihaz/e-Adisyon kuralları, bir promosyonel/ikram satırının
gerçek brüt değerini gizlemeden, ayrı bir indirim satırı olarak göstermesini
gerektirir; "hiç satılmamış gibi" bir sıfır satır bu kuralı ihlal eder ve
denetimde işletmenin gerçek ciro/ikram oranını gizler.

## Deliverables

- `docs/domain/complimentary-line-fiscal-representation.md`: seçilen sonuç,
  reddedilen alternatif (sessizce sıfırlama), en az iki pozitif/negatif
  örnek, tüketici görevler için invariant listesi.

## Acceptance evidence

- Karar kaydı, `V1-RMD-228`'in gerçek kodunun (`BillItem.cs`) davranışıyla
  birebir tutarlı (kod zaten Done, karar yalnız onu resmileştiriyor).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V13-FSC-001
