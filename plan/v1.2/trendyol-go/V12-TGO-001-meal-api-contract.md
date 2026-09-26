# V12-TGO-001 - Uber Eats Trendyol Go yemek API sözleşmesini doğrula

- Task ID: V12-TGO-001
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-09-26
- EXT:TGO-MEAL-API

## Goal

Uber Eats Trendyol Go yemek entegrasyonunun kimlik doğrulama, webhook, sipariş durumu, iptal ve menü
sözleşmesini gerçek satıcı hesabı ve test (stage) ortamıyla doğrulamak.

## Owned surface

- `evidence/V12-TGO-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Basic auth (satıcı numarası, API anahtarı ve gizli anahtar), zorunlu `User-Agent` ile `x-agentname` ve `x-executor-user` başlıkları, 50 istek/10 sn sınırı.
2. Webhook entegratörünün oluşturulması ve etkinleştirilmesi, olay türleri, 3 denemelik yeniden gönderim ve entegrasyonun kapatılma koşulu.
3. Test siparişi servisiyle Created → Picking → Invoiced → Shipped → Delivered akışı, `UnSupplied` iptal nedenleri (621–627), menü ürün durumu ve fiyat güncelleme sonucu.
4. Uber geçiş modeli: 5 karakterli sipariş kodu, maskeli adres, otomatik `Invoiced`.

## Out of scope

- Kod; uygulama V12-TGO-002..005'te.

## Dependencies

- V12-GOV-006

## Blocker

- Satıcı panelindeki "Hesap Bilgilerim → Entegrasyon Bilgileri" sayfasından alınacak stage ve canlı API bilgileri çalışma alanında yok.
- Görev ancak restoranın Uber Eats Trendyol Go satıcı hesabı ve entegrasyon bilgileri sağlandığında `Planned` olur.

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-TGO-001/` altında.
- `task_scope_tool.py --task-id V12-TGO-001 --diff-base <InProgress commit>` exit 0.
- Gerçek stage erişimiyle alınmış tarihli istek/yanıt ve webhook kayıtları; en az bir hata çıktısı.

## Handoff

- None
