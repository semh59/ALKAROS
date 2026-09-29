# V1-RMD-441 - Cari tahsilat zincirinin (V14-ACC-004, 005, 009) başlatılması

- Task ID: V1-RMD-441
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: governance
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

Semih'in kararı (2026-09-29): müşteri hesabı arayüzü, planlı arka uç zinciriyle ve her görev ayrı PR'da yapılır; kartla
cari tahsilat (Token/Beko cihazını bekleyen V14-ACC-006/007) ve mali belgeye bağlı "hesaba yaz" yönlendirmesi
(V14-ACC-008) şimdilik dışarıda kalır.

Kapsam denetimi, master'da Planned olan bir görevin dosyasında bir PR'da yalnız Status/Assignee satırlarının
değişmesine ve Owned surface'in master'daki hâlinden okunmasına izin verir. Bu yüzden zincirin görevleri önce bu
yönetim göreviyle başlatılır: V14-ACC-004, V14-ACC-005 ve V14-ACC-009 `InProgress` olur, uygulamanın dokunması
gereken paylaşılan dosyalar (migration manifesti, modül kaydı, çözüm dosyası, kilit dosyaları, erişilemez servis
listesi) "Sınırlı ek" olarak eklenir ve V14-ACC-005'in uygulama kararları (adisyondan bağımsız tahsilatın
`payments.payments` satırı üretmemesi, kasaya `CashIn` hareketi, fazla ödeme reddi) kayda geçer. Uygulama ve Done
geçişi her görev için master'a birleştirmeden sonra ayrı PR'da gelir.

## Owned surface

- `plan/v1/remediation/V1-RMD-441-start-v14-account-receipt-chain.md`
- `evidence/V1-RMD-441/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1.4/customer-account/V14-ACC-004-account-payment-posting.md,
  plan/v1.4/customer-account/V14-ACC-005-cash-account-receipt.md ve
  plan/v1.4/customer-account/V14-ACC-009-independent-account-receipt.md — yalnız Status, Assignee, Sınırlı ek
  satırları ve V14-ACC-005'in uygulama kararları

## In scope

- Üç görev dosyasının başlatılması; kod değişikliği yok.

## Out of scope

- V14-ACC-006, 007, 008 ve V14-UI-001 (bağımlılıkları dış kararları bekliyor).

## Dependencies

- V1-RMD-440

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` ve `python tools/consistency-audit/consistency_audit.py`
  0 hata / 0 uyarı (`evidence/V1-RMD-441/audit.log`).
- Zincirin kodu hazır ve yerelde yeşil: V14-ACC-004 25/25, V14-ACC-005 11/11 (gerçek PostgreSQL 18); her biri kendi
  PR'ında kendi kanıtıyla gelir.
- Semih'in elle deneyebileceği senaryo: yok; yalnız plan kaydı.

## Handoff

- V14-ACC-004
