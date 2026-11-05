# V1-RMD-830 - Tahsilat sayfası: indirim ve bahşiş geri alma

- Task ID: V1-RMD-830
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

K18 arayüz tarafı (sunucu ucu V1-RMD-829'da Done). Tahsilat sayfasında "Uygulanan indirim, bahşiş ve ücretler" listesinde her indirim ve bahşiş satırının yanında "Geri al" düğmesi çıkar; yanlış girilen kayıt tek dokunuşla kalkar, ödenecek tutar yenilenir. Hizmet bedeli, kuver ve özel ücret satırında düğme yoktur (sunucu da reddeder). Olmayan kayıt (404, başka terminalde zaten kaldırılmış) listeyi yeniler ve bunu söyler; hesap durumu çakışması (409) ve toplanan tutarın altına inme Türkçe cümleyle gösterilir; hata `role="alert"` ile duyurulur. `ADJUSTMENT_UNDO_BELOW_COLLECTED` sözlük cümlesi V1-RMD-803'te eklendi; bu görev yalnız doğrular.

## Owned surface

- `evidence/V1-RMD-830/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V1-RMD-264) — Geri al düğmesi ve işleyici.
  - tests/Clients/StaticApps/split-payment-undo-adjustment.test.js (V1-RMD-109) — geri alma testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.

## Out of scope

- Sunucu ucu (V1-RMD-829); React kasa.

## Dependencies

- V1-RMD-829
- V1-RMD-803

## Acceptance evidence

1. StaticApps testleri yeşil: indirim ve bahşiş satırında Geri al çıkar, hizmet/kuver/özel ücret satırında çıkmaz; basınca doğru uca istek gider, liste ve toplam yenilenir; 404, 409 durum, toplanan tutarın altı ve ağ hatası Türkçe mesajla gösterilir; çift tıklama tek istek atar; `error-dictionary-lock` yeşil.
2. Mutasyon: tür koşulu, istek adresi, yenileme, hata gösterimi, meşgul kilidi bozulunca test kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
