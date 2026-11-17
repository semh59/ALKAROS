# V1-RMD-1145 - Kasa tutar alanlarında kutunun tamamına dokunmak odaklar (48 px hedef)

- Task ID: V1-RMD-1145
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 6): "Tutar" ve "Alınan para" (Hesap Ödeme) ile "Nakit giriş/çıkış tutarı", "Sayılan Tutar" ve "Açılış Tutarı" (kasa oturumu) alanlarında dış kutu 48 px ama içindeki yazı alanı 30-35 px yüksek; kutunun kenarına ya da ₺ simgesine dokunmak alanı odaklamıyor. `docs/CASHIER_UI_RULES.md` her dokunulan öğe için en az 48 px ister.

1. Kutu (`.sp-amount-field`, `.cs-amount-field`) bir `<label>` olur: içindeki yazı alanıyla örtük ilişkilidir, kutunun herhangi bir yerine (kenar boşluğu, ₺ simgesi dahil) dokunmak alanı odaklar; yazı alanının erişilebilir adı (`aria-label`) ve kimliği aynen kalır.
2. Kutu `min-height: var(--touch-target)` bildirir ve yazı alanı kutunun yüksekliğini doldurur (dikey iç boşluk kutudan yazı alanına geçer); kutunun görünümü (kenarlık, yazı boyutu) değişmez. Alçak ekran kuralındaki (`max-height: 860px`) azaltılmış iç boşluk 48 px altına inmez.
3. Kural testle sabitlenir: iki kutu kuralı dokunma hedefi testinin listesine girer; kutuların `<label>` olması sayfa betiklerinin kaynağından denetlenir. Gerçek yükseklik canlı tarayıcıda (`getBoundingClientRect`) ölçülür ve kanıt klasörüne yazılır; jsdom yerleşim bilmediği için tek başına yeterli sayılmaz.

## Owned surface

- `evidence/V1-RMD-1145/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/cashier-amount-field-label.test.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css
  - tests/Clients/StaticApps/cashier-touch-targets.test.js
  - tests/Clients/StaticApps/split-payment-table-note.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps) ve canlı yükseklik ölçümü

## Out of scope

- Başka alan türleri (metin, seçim kutuları); PosTerminal React ekranları; Cashier dışındaki sayfalar; yazı boyutu ve renk değişiklikleri

## Dependencies

- V1-RMD-1144

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0 ve canlı ölçüm çıktısı

## Handoff

- None
