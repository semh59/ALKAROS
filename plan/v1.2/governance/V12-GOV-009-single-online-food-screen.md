# V12-GOV-009 - Online yemeğin tek ekrandan yönetilmesi kararı ve görev planı

- Task ID: V12-GOV-009
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Semih 2026-09-27'de online yemeğin tek ekrandan kullanılmasını istedi: sipariş kısmı da menü gibi ayarlar da aynı
yerden. Bugün sipariş kuyruğu ve platform bilgileri iki ayrı ekranda; menü yayını, ürün eşleme ve restoranı
platformda açıp kapatma için ekran yok. Bu görev kararı kaydeder ve işi uygulanabilir görevlere böler.

## Owned surface

- `plan/v1.2/governance/V12-GOV-009-single-online-food-screen.md`
- `plan/v1.2/online-operations-ui/V12-OUI-004-online-food-hub.md`
- `plan/v1.2/online-operations-ui/V12-OUI-005-online-menu-tab.md`
- `plan/v1.2/online-operations-ui/V12-OUI-006-online-problems-tab.md`
- `plan/v1.2/online-ordering/V12-ONL-011-platform-store-status.md`
- `evidence/V12-GOV-009/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-27 kararı):
  - plan/OFFICIAL_SOURCE_REGISTER.md — Yemeksepeti ve Trendyol Go kaynak satırlarının restoran durumu kapsamı.
  - plan/TRACEABILITY.md — `C108` kaydı.
  - plan/v1.2/README.md — yeni görevlerin listesi.

## In scope

1. Karar kaydı (`evidence/V12-GOV-009/decision-record.md`): Semih'in dört cevabı, sekmeler, yetki, platform
   servisleri ve kaynaklar.
2. Dört görev dosyası: `V12-OUI-004` (merkez ekran), `V12-OUI-005` (Menü sekmesi ve ürün eşleme), `V12-OUI-006`
   (Sorunlar sekmesi), `V12-ONL-011` (restoranı platformda açma/kapama/yoğun).
3. Kaynak defterinde iki platformun restoran durumu servislerinin kapsama eklenmesi ve `C108` kaydı.

## Out of scope

- Kod; her görev kendi Task ID'si ile uygulanır.
- Migros Yemek (belge yok).

## Dependencies

- V12-GOV-008

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-27: "online yemek tek ekrandan"; sekmeler Siparişler, Menü,
Sorunlar, Ayarlar; "Menü ve ayarlar yönetici şifresiyle giriş yapılırsa görünür olsun"; restoranı platformda
kapatma/yoğun "Evet, bu işe dahil et"; iki eski giriş "Tek girişte birleşsin"; zaman "Şimdi, Faz 4'ten önce".

## Deliverables

- Karar kaydı, dört görev dosyası, kaynak defteri ve `C108`.

## Acceptance evidence

- `plan_audit_tool.py validate` 0 hata / 0 uyarı; `consistency_audit.py` temiz.
- `task_scope_tool.py --task-id V12-GOV-009 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
