# V13-PUI-004 - EFT/Havale ödeme ekranı

- Task ID: V13-PUI-004
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-16

## Goal

V13-PUI-001'in ödeme ekranına (Nakit/Kredi Kartı/Yemek Kartı) dördüncü bir
yöntem kartı eklemek: EFT/Havale. Tasarım taslağı zaten review edildi
(kasa-onizleme artifact, 2026-09-16): kasiyer EFT'yi seçtiğinde bir onay
kutusu çıkar ("Tutarı işletmenin banka hesap hareketinde gördüm"), bu
işaretlenmeden ödeme uygulanmaz. Referans numarası alanı YOK (Semih'in
tercihi).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/index.html,
  src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js,
  src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  (V13-PUI-001 sahipliğinde kalır — orijinal task taslağındaki
  `src/Clients/Cashier/Payments/SplitPayment/**` yolu, bu ekranın gerçek
  ALKAROS'ta bir C# namespace'i değil, WaiterPwa/`cash-session` ile aynı
  desende düz HTML/CSS/JS sayfası olarak yaşadığını yansıtacak şekilde
  V13-PUI-001'in kapanışında düzeltilmişti; bu görev de o gerçek yolu
  kullanır) — yalnız dördüncü (EFT zaten var olan bir yöntem kartıydı,
  buraya eklenen asıl şey) onay kutusu ve onun "Ödemeyi Ekle" düğmesini
  kilitleme mantığı eklendi; Cash/BankCard akışları değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  (V13-PUI-001 sahipliğinde kalır) — yalnız EFT-tek ödemeyle hesabın tam
  kapandığını (remainingAmount == 0) doğrulayan yeni bir test eklendi.
- `evidence/V13-PUI-004/**`

## In scope

- Dördüncü tender kartı: "EFT/Havale".
- Seçildiğinde görünen onay kutusu; işaretlenmeden "Ödemeyi Ekle" pasif
  kalır (foundations.md'nin disabled-buton kuralına uygun).
- Para üstü gösterimi YOK (V13-PAY-005'in kuralıyla tutarlı — asla aşamaz).
- Vardiya kapalıyken de bu kart aktif kalır (Nakit'in aksine, bkz.
  V13-PAY-005 In scope) — V1-WTR-055/V1-CUI-010'un vardiya-kapalıyken-
  nakit-kapanır davranışıyla KARIŞTIRILMAZ.

## Out of scope

- Referans numarası veya dekont fotoğrafı yükleme.
- Banka API entegrasyonu.

## Dependencies

- V13-PAY-005
- V13-PUI-001

## Acceptance evidence

- **Gerçek durum notu (kapanışta bulundu):** EFT yöntem kartının kendisi
  zaten V13-PUI-001'de vardı (`METHOD_LABELS.Eft`, seçilebilir chip,
  vardiya kapalıyken de aktif — `renderMethodChips()` yalnız `Cash`'i
  `!state.cashSessionOpen` durumunda pasifleştiriyor, `Eft` hiçbir zaman
  pasifleşmiyor; bu görev bunu YENİDEN doğruladı, değiştirmedi). Bu
  görevin asıl teslimatı: (1) EFT seçiliyken görünen onay kutusu
  ("Tutarı işletmenin banka hesap hareketinde gördüm"), (2) bu kutu
  işaretlenmeden "Ödemeyi Ekle" düğmesinin pasif kalması, (3) sunucuya
  gönderim öncesi aynı kuralın ikinci (savunma amaçlı) bir kontrolü.
  Referans numarası alanı zaten yoktu (yalnız opsiyonel serbest metin
  "Not" alanı), para üstü zaten hiçbir yöntem için gösterilmiyordu —
  ikisi de değiştirilmeden doğrulandı.
- `node --check src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js`
  → sözdizimi hatasız.
- Gerçek Postgres'e karşı (`ALKAROS.Host.Experience.PaymentTender.Tests`,
  `alkaros-test-pg`, port 55432): 11/11 yeşil (10 önceki V13-PUI-001 testi
  - yeni `EftOnlyTenderFullyClosingTheBillReflectsZeroRemainingAmount` —
  iki ayrı EFT tahsilatıyla (50+25=75) bir hesabın `remainingAmount`
  alanının sunucu tarafında tam sıfıra ulaştığını, tek bir client-side
  hesaplamaya değil sunucunun kendi GET özetine dayanarak kanıtlıyor —
  bu, ekranın "Ödendi" fazına geçiş sinyaliyle birebir aynı gerçek veri
  yolu).
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- **Doğrulanamayan (dürüstçe belirtiliyor):** onay kutusunun gerçek bir
  tarayıcıda fare/dokunmatikle işaretlenip düğmenin görsel olarak
  etkinleştiğini bizzat bir tarayıcı sürücüsüyle (Playwright vb.) izleyerek
  doğrulamadım — yalnız `node --check` (sözdizimi) ve altta yatan sunucu
  mantığının gerçek HTTP testleriyle doğrulanması yapıldı. Semih'in kendi
  elleriyle deneyebileceği senaryo (bir hesabı EFT ile tam kapatmak) sunucu
  tarafında kanıtlandı; ekranın kendisinde gerçek tıklama/klavye
  etkileşimi bu oturumda gözlemlenmedi.
