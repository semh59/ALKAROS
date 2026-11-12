# V1-RMD-998 - Masa listesi garson, kişi sayısı, bölge adı ve hesap istedi bilgisini taşır

- Task ID: V1-RMD-998
- Status: Planned
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa listesi sunucu yanıtı, yeni ana ekranın masa kartı için gereken alanları taşır: bölge adı, masayı servis eden garsonun adı, kişi sayısı ve masanın hesabını kasaya gönderdiği bilgisi (hesap istedi). Hesap istedi bilgisi yalnız kasaya gönderilmiş ve henüz ödenmemiş bir hesaptan türetilir; bağlanamıyorsa ekranda gösterilmez.

## Owned surface

- `evidence/V1-RMD-998/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - Başlangıç commit'inde bu listeye gerçek dosya yolları yazılır (plan commit'i ayrı).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Masa listesi uç noktası (GET tables) bölge adı, garson adı, kişi sayısı ve hesap istedi bayrağı döndürür; birleşik masada ve boş masada alanlar doğru gelir.
2. Kırmızı test: alanlar yokken başarısız; mutasyon: bayrak koşulunu bozmak testi kırar.

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Koyu tema

## Dependencies

- V1-RMD-997

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları (istemci değişiyorsa)
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None
