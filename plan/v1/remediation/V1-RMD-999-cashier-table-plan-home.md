# V1-RMD-999 - Masa planı kasa ana ekranı: bölge sekmeleri, zengin masa kartı, dikkat şeridi

- Task ID: V1-RMD-999
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masalar ekranı (kasiyerin açılış ekranı) taslak v4'e göre masa planıdır: bölge sekmeleri, masa araması, durum için ikon ile desen ve renk birlikte, masa kartında ad, tutar, süre, garson ve kişi sayısı, gerçek veri kaynağı olan durumlar için dikkat şeridi (hesabı açık masa için Ödeme bekliyor; kasaya gönderilmiş ve henüz faturalanmamış hesap için Hesap istedi, Bekleyen hesaplar listesinden masa kimliğiyle eşlenir). Liste görünümü varsayılandır; kat planı seçenek olarak kalır. Giriş sonrası kasiyer bu ekrana düşer. Paket düğmesi eklenmez: üründe paket siparişi diye bir akış yok. Yükleniyor, boş, hata, bağlantı yok ve bayat durumları mevcut durum ekranlarıyla sürer.

## Owned surface

- `evidence/V1-RMD-999/**`
- `src/Clients/PosTerminal/src/features/tables/tablePlanModel.ts`
- `src/Clients/PosTerminal/src/features/tables/tablePlanModel.test.ts`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/models.ts
  - src/Clients/PosTerminal/src/features/tables/tables.css
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her durum için ikon, desen ve renk testi; Ödeme bekliyor ve Hesap istedi eşlemesi; tutar, süre, garson, kişi sayısı; dikkat şeridi; bölge sekmeleri; liste varsayılan.
2. Hesap istedi bilgisinin yüklenemediği durumda ekranın bozulmaması (şerit ve durum yalnız bilinenleri gösterir).
3. Üçüncü deneme #8: kasiyer rolü, 768x1024, masa birleştirme onayı, taşınmış siparişin Bekleyen hesaplar durumu, kasa kapandıktan sonra durum doğrulaması 1001 sonunda ekran görüntüsüyle yapılır ve kanıta yazılır.

## Out of scope

- Sağ panel (1001), üst başlık (1000), ödeme ekranı (1002), koyu tema, Paket

## Dependencies

- V1-RMD-998

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None
