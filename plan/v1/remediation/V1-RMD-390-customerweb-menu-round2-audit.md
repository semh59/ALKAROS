# V1-RMD-390 - CustomerWeb Menu Tur 2 denetimi: bulgu yok (kod değişikliği gerektiren)

- Task ID: V1-RMD-390
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 15: CustomerWeb'in
QR Menü sayfası (`menu-app.js`/`index.html`). Sayfa zaten çok yalın ve girişsiz — kategori
sekmeleri, ürün kartı, sabit-konumlu sepet çubuğu — P4 (öğrenme eşiği) açısından sorunsuz.

Tek gerçek gözlem yeni değil: `menu-app.js`'in "Sepete ekle" akışı, `Cashier.tsx`'in Modül 5'te
(V1-RMD-380) tespit edilen aynı kök nedenden ötürü hiçbir modifikatör grubu (boy/ekstra vb.)
sunmuyor — çünkü QR menü de aynı `DualScreenApplication`/`DualScreenContracts` domain modelinden
besleniyor ve o domain'de modifikatör kavramı hâlâ yok. Ama burada iki hafifletici fark var:
(1) `OrderEntry` sayfası zaten her sepet satırı için serbest metin "Not ekleyin" alanı sunuyor —
bir müşteri "büyük boy" gibi bir talebi buraya yazabilir; (2) bu zaten V1-RMD-380'de en yüksek
öncelikli ertelenmiş madde olarak Semih'e açıkça bildirildi. Aynı backend değişikliğini gerektiren
bir bulguyu burada ikinci kez ayrı bir görev olarak açmak yerine, bu tekrarı burada belgelemek
daha dürüst.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-390-customerweb-menu-round2-audit.md`

## In scope

- Yok — kod değişikliği yapılmadı.

## Out of scope

- Modifikatör desteği: [[V1-RMD-380]] (Modül 5) ile aynı kök neden — backend domain/şema
  değişikliği gerektiriyor, Semih'in kararına bırakıldı (o görevde zaten en yüksek öncelikli
  madde olarak işaretlendi).
- Arama kutusu yokluğu: kategori sekmeleriyle zaten gezilebilir bir liste; somut bir saha
  şikayeti/kanıtı olmadan spekülatif bir P1 iyileştirmesi eklemek bu turun "gerçek bulgu"
  standardına uymuyor.

## Dependencies

- None

## Acceptance evidence

- Kod değişikliği yapılmadığı için test/mutation-check gerekmedi.

## Handoff

- None
