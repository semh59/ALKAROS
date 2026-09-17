# V13-GOV-004 - Decide that card tip stays out of V1.3 scope

- Task ID: V13-GOV-004
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-17

## Goal

Kart üzerinden bahşiş toplamanın (card tip) V1.3 kapsamına ALINMAYACAĞINI
resmi bir karara bağlamak. Nakit bahşiş zaten `V0-CMP-004`'ün onaylı
kararı kapsamında (bkz. o kaydın "Tip" satırı) — bu karar onu DEĞİŞTİRMEZ,
yalnız KART kanalını V1.3'te kapatır.

## Owned surface

- `docs/domain/card-tip-scope-decision.md`
- `evidence/V13-GOV-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kart bahşişinin V1.3'te neden desteklenmediğinin gerekçesi ve hangi
  koşullar oluşunca yeniden değerlendirileceği.

## Out of scope

- Nakit bahşiş — `V0-CMP-004` kapsamında zaten karar altında, bu görev onu
  değiştirmez.
- Kart bahşişini gelecekte (V1.3 sonrası) nasıl uygulayacağımızın tasarımı
  — koşullar netleşmeden tasarlanmaz.

## Dependencies

- V0-CMP-004

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-17 ("Kart bahşişi —
hem Token teknik olarak hazır değil hem de Türkiye'de yasal düzenleme
(Dijital Gönüllü Bahşiş Sistemi) henüz çıkmadı. Nakit bahşişle devam,
kartı ayrı bir 'yasal netleşince' notuna yaz"). Gerekçe iki katmanlı:

1. **Teknik:** `developer.tokeninc.com`'un kamuya açık `IntegrationHub.dll`/
   `TokenX Connect` dokümantasyonu bahşişe özel bir API yüzeyi
   göstermiyor (yalnız `type`/`operatorId` ile ödeme türü/yemek kartı
   operatörü seçimi belgeleniyor) — `V0-HUG-001`'in kendi doğrulaması bu
   konuda henüz bir bulgu üretmedi.
2. **Yasal:** Türkiye'de kart üzerinden bahşiş toplanmasına ilişkin
   düzenleyici çerçeve ("Dijital Gönüllü Bahşiş Sistemi") henüz
   yürürlüğe girmedi (Semih'in kendi bilgisi, 2026-09-17). Netleşmeden
   önce bir uygulama yazmak, kurallar netleştiğinde sökülmesi veya
   uyumsuz kalması riski taşır — aynı oturumda zaten belgelenen 2026-01-30
   tarihli zorunlu servis/kuver ücreti yasağı emsali gibi, bu alan hızlı
   değişebilir.

## Deliverables

- `docs/domain/card-tip-scope-decision.md`: karar, gerekçe, yeniden
  değerlendirme koşulları.

## Acceptance evidence

- Karar kaydı, `V0-CMP-004`'ün nakit bahşiş kararını değiştirmediğini ve
  yalnız kart kanalını kapattığını açıkça belirtir.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
