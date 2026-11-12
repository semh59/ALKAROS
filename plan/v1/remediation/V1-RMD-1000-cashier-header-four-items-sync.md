# V1-RMD-1000 - Kasa üst başlığı dört öğe ve bağlantı göstergesi

- Task ID: V1-RMD-1000
- Status: Planned
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Üst başlık dört öğe taşır: Masalar, Bekleyen hesaplar (rozetli), Kasa durumu, Diğer; yanında bağlantı ve son güncelleme göstergesi ile kullanıcı adı bulunur. 977 görevinin duraklatılmış başlık çalışması (stash) bu görevde tamamlanır ve 977 bu görevle kapatılır. Kısa ekranda başlık taşmaz.

## Owned surface

- `evidence/V1-RMD-1000/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - Başlangıç commit'inde bu listeye gerçek dosya yolları yazılır (plan commit'i ayrı).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Dört öğe ve gösterge testleri; Diğer menüsü Mutfak, Müşteri ekranı, Cari hesaplar, Rezervasyon, Ürün menüsü, Yönetim, Çıkış öğelerini içerir; yetkisiz rol öğeyi görmez.
2. Bağlantı yoksa gösterge uyarı verir; son güncelleme süresi eskirse bayat uyarısı çıkar.

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Koyu tema

## Dependencies

- V1-RMD-999

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları (istemci değişiyorsa)
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None
