# V1-RMD-999 - Kasa ana ekranı masa planıdır

- Task ID: V1-RMD-999
- Status: Planned
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer girişinde açılan ilk ekran masa planıdır: bölge sekmeleri, masa arama, durum (ikon, desen ve renk birlikte), masa kartında ad, tutar, süre, garson ve kişi sayısı, yalnız gerçek veri kaynağı olan durumlar için dikkat şeridi (Boş, Dolu, Rezerve, Temizleniyor, Servis dışı, hesabı açık masa için Ödeme bekliyor, 998 bağlanırsa Hesap istedi). Yükleniyor, boş, hata ve bağlantı yok (eski veri) durumları gösterilir. Hızlı satış ve Paket çerçeveli ikincil düğmelerdir; hızlı satış ekranı ayrı rotada kalır. Tasarım sistemi (--ds-*) ve Manrope kullanılır, koyu tema yoktur. Taslak: scratchpad/kasa-ana-ekran-taslagi.html (v4).

## Owned surface

- `evidence/V1-RMD-999/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - Başlangıç commit'inde bu listeye gerçek dosya yolları yazılır (plan commit'i ayrı).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Giriş sonrası / rotası masa planını açar; hızlı satış kendi rotasında çalışır, mevcut Cashier testleri güncellenir.
2. Her durum için ikon+desen+renk testi; arama ve bölge süzgeci testi; yükleniyor/boş/hata/eski veri testleri.
3. Üçüncü deneme #8: kasiyer rolüyle giriş, 768x1024, masa birleştirme onayı, taşınmış siparişin Bekleyen hesaplar durumu, kasa kapandıktan sonraki durum doğrulanır ve kanıta yazılır.

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Koyu tema

## Dependencies

- V1-RMD-998

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları (istemci değişiyorsa)
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None
