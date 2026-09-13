# V1-KIT-011 - Yerel ağ dayanıklılığını doğrulanmış bir fark olarak belgele

- Task ID: V1-KIT-011
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: documentation
- Surface state: Existing

## Goal

Rakip KDS araştırması (2026-09-13, `[[kitchen-redesign-requirements]]`):
cloud-öncelikli rakiplerin (Toast/Square/Lightspeed) ortak yapısal zaafı
— internet kesilince mutfağa sipariş gitmiyor (endüstri kaynağı: 45
dakikalık bir kesinti tek başına 1.100 USD+ doğrudan satış kaybına yol
açabiliyor). `docs/architecture/deployment-compatibility-matrix.md`
(V0-ARC-007, Semih onaylı) doğrulandı: ALKAROS **yerel ağda self-hosted**
("Local backend", "local network" ön koşulu) — PosTerminal/WaiterPwa
bulut değil LAN üzerinden Host'a konuşuyor. Bu, kod değişikliği
gerektirmeyen, zaten var olan bir mimari karar ama hiçbir yerde rakip
karşılaştırması bağlamında **doğrulanmış bir fark** olarak kayıtlı değil
— bu görev yalnız bunu belgeler, yeni bir davranış eklemez.

## Owned surface

- docs/architecture/deployment-compatibility-matrix.md (Sınırlı ek —
  V0-ARC-007 sahipliğinde kalan, Semih onaylı karar kaydı) — mevcut
  "Local backend"/"local network" satırlarına, bunun rakip
  karşılaştırmasında doğrulanmış somut faydasını (internet kesintisinde
  mutfak/POS çalışmaya devam eder, cloud-öncelikli rakiplerin aksine) not
  düşen kısa bir paragraf. Karar/rakamlar değişmez, yalnız açıklayıcı not
  eklenir.

## Out of scope

- Yeni bir davranış/kod değişikliği — bu mimari zaten var, yalnız
  belgeleniyor.
- Pazarlama/satış materyali üretimi — bu mühendislik dokümantasyonu,
  ayrı bir iş.

## Dependencies

- None

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- Semih'in elle doğrulayabileceği senaryo: `docs/architecture/deployment-compatibility-matrix.md`'yi
  aç, yeni §4'ün mevcut §1-3 karar tablosuyla çelişmediğini, yalnız
  açıklayıcı olduğunu gör.

## Handoff

- None
