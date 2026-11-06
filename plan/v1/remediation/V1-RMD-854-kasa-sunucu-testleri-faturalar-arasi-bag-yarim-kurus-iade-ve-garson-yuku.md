# V1-RMD-854 - Kasa sunucu testleri: faturalar arası bağ, kalem iadesinde yarım kuruş, garson yükü sıfırdan büyük

- Task ID: V1-RMD-854
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (F0048, F0437/H08, F0385, F0917): üç sunucu davranışı testsiz. (1) Migration 032'nin asıl garantisi, başka faturanın kalemine ödeme dağılımı/düzeltme bağlanamaması (bileşik yabancı anahtar) yalnız bilinmeyen kimlikle sınanıyor; gerçek ihlal (B faturasına A'nın kalemi) sınanmıyor. (2) Kalem iadesinde tutar `decimal.Round(brüt * adet / satırAdedi * oran, 2, AwayFromZero)` ile hesaplanıyor; kesin yarım kuruşu üreten girdiyle yuvarlama yönü sınanmıyor (V1-RMD-737 H08 'öldürülemedi' diyor, oysa ondalık işlemle kesin yarım kuruş üretilebilir). (3) `waiter-load` ucu yalnız `ActiveLoad == 0` doğruluyor; garsona atanmış açık siparişlerin sayıldığı, kapalı/iptal edilenin sayılmadığı sınanmıyor. (4) Kısmi iadeli nakit ödemenin kasa oturumu kapanışında beklenen nakde etkisi (V1-RMD-522 'doğrulanmadı') çalıştırılarak sınanır. Üretim kusuru bulunursa kırmızı olur ve ayrı göreve devredilir.

## Owned surface

- `evidence/V1-RMD-854/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Modules/Billing/SplitDesign/PostgresSplitDesignTests.cs — başka faturanın kalemine dağılım -> 23503.
  - tests/Modules/Billing/Adjustments/PostgresAdjustmentsTests.cs — başka faturanın kalemine düzeltme -> 23503.
  - tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj — test veritabanına bileşik bütünlük migration'ı (032) eklenir.
  - tests/Modules/Billing/Adjustments/ALKAROS.Billing.Adjustments.Tests.csproj — test veritabanına bölme tasarımı (020) ve bileşik bütünlük (032) migration'ları eklenir.
  - tests/Host/Experience/Billing/RefundHalfPortionHttpTests.cs — kesin yarım kuruş iade yuvarlaması.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs — garson yükü sıfırdan büyük.
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs — kısmi iadeli nakit sonrası kapanış beklenen nakit.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Modül test veritabanları migration 032'yi (bileşik yabancı anahtar) içermiyordu, bu yüzden garanti hiçbir testte görünmüyordu; iki test projesine 032 (ve ona gereken 020) eklenir.
2. Önce kırmızı/yeşil keşif: her test yazılır; geçerse mutasyonla gücü kanıtlanır, düşerse kusur ayrı göreve devredilir.
3. Mutasyon: bileşik yabancı anahtar düşürülür (test DB'de), yuvarlama yönü AwayFromZero -> ToEven, yük sorgusunun durum süzgeci, kapanışta iade düşümü kaldırılır (cp+cmp ile geri alınır).

## Out of scope

- Üretim kodu ve migration'lar; H18 (etkin vergi oranı yuvarlaması: kod V1-RMD-730'da `BillMath.Apportion`'a taşındı, ayrı yüzey kalmadı).

## Dependencies

- None

## Acceptance evidence

1. İlgili test projeleri gerçek Postgres (alkaros-test-pg, 55432) ile exit 0; çıktılar `evidence/V1-RMD-854/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
