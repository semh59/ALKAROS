# V1-RMD-1059 - İade ve iptal formunda alanlar görünür etiket taşır, yıkıcı onay düğmesi masa adını ve tutarı söyler ve pasif düğmenin nedeni yazılır

- Task ID: V1-RMD-1059
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Hesap Ödeme sayfasının iade ve iptal bölümünde gerekçe, ödeme ve stok seçimleri etiketsiz üç açılır liste olarak yan yana duruyor (etiket yalnız ekran okuyucuya yazılı), yıkıcı onay düğmeleri ("Evet, hesabı iptal et", "Evet, iadeyi onayla") hangi masayı ve ne kadar tutarı etkilediğini söylemiyor ve onaylı iadeyi iptal düğmesi gerekçe girilmeden pasifken nedeni yazılmıyor. docs/CASHIER_UI_RULES.md'ye göre: her açılır liste görünür etiket taşır, yıkıcı onay düğmesi eylemi, masayı (ya da Hızlı satış) ve tutarı adlandırır, pasif düğmenin altında nedeni yazan cümle çıkar. "Siparişi de iptal et" kutusu bu görevde varsayılan boş kalır (kod doğrulandı), değiştirilmez.

## Owned surface

- `evidence/V1-RMD-1059/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/split-payment-refund-rules.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Cashier sayfaları için tests/Clients/StaticApps testleri

## Out of scope

- Listede olmayan ekranlar ve dosyalar; durum başlığı simgelerinin (✓ ve …) birleştirilmesi (ürün kararı)

## Dependencies

- V1-RMD-1058

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. tests/Clients/StaticApps tam koşusu exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
