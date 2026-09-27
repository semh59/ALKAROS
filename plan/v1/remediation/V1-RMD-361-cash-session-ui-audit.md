# V1-RMD-361 - Kasa Oturumu modül denetimi: hata rolü, sekme ARIA'sı, faz-geçişi odak yönetimi

- Task ID: V1-RMD-361
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 2. modülü: Cashier'ın Kasa
Oturumu tekil-sayfa akışı (`src/Clients/Cashier/wwwroot/payments/cash-session/**` — aç/nakit hareketi/say/
kapat/mutabakat-fark-teyidi). On iki boyut üzerinden tarandı; üç bağımsız, gerçek bulgu tespit edildi ve
düzeltildi. Bu modül, önceki oturumlarda çok sayıda bağımsız denetimden geçmiş (V1-RMD-234/241/291/314/343)
ve genel olarak çok sağlam durumda — bu geçişte kalan boşluklar dar ve teknikti.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/24-cash-session-accessibility.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-361-cash-session-ui-audit.md`

## In scope

1. **[T1, Orta] Hata kutusu `role="alert"` taşımıyordu.** `#app` zaten `aria-live="polite"`
   olduğu için ekran okuyucu metni yine de duyuyordu, ama bu kutunun özellikle bir HATA olduğunu
   (yalnızca "içerik değişti" değil) işaretleyen `role="alert"` yoktu — CustomerWeb'in aynı sınıftan
   boşluğunu kapatan V1-RMD-345'in bu dosyaya hiç uğramadığı görüldü.
2. **[T1, Düşük] "Giriş"/"Çıkış" (cs-tab) sekmelerinde `role="tab"`/`aria-selected` yoktu.**
   Modül 1'in kategori sekmesi bulgusuyla (V1-RMD-360) aynı desen, ama burada CSS zaten doğru
   stilleniyordu — eksik olan yalnızca ARIA durumuydu.
3. **[P2, Orta — saha gerçekliği] Faz geçişlerinde (login→açılış→hareket→sayım→kapatma) hiçbir
   alana otomatik odaklanma yoktu.** Dokunmatik-öncelikli bir kiosk'ta bu, sayısal klavyenin
   kendiliğinden açılmaması demek — kasiyer her yeni ekranda önce tutar alanına dokunmak zorunda
   kalıyordu. `render()`'a, faz GERÇEKTEN değiştiğinde (yalnızca `setBusy` gibi aynı ekranın
   yeniden çizimlerinde değil) yeni ekranın ilk giriş alanına (yoksa ilk etkin düğmeye) odaklanan
   tek bir kontrol noktası eklendi.

## Out of scope

- P1 (rakip karşılaştırması): sayım ekranı tek bir "sayılan tutar" alanı istiyor, bazı rakip
  ürünler kupür bazlı (100₺/50₺/... adet) bir sayım arayüzü sunuyor (toplamı sistem hesaplıyor,
  kasiyer zihinden toplama yapmıyor). Gerçek bir gözlem ama büyük bir özellik eklemesi — kod
  değişikliği yapılmadı, Semih'in kararına bırakıldı.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier` tam paketi (56 test, 24 numaralı yeni dosya dahil): 56/56 geçti, regresyon
  yok.
- Mutation-check: `cash-session.js` `git stash` ile geri alındı, yeni 24 numaralı spesifikasyonun
  2 test durumu da GERÇEKTEN kırmızı oldu (role="alert" eksik, birincil alan odaklanmadı).
  `git stash pop` ile geri yüklendi, paket tekrar 56/56 yeşile döndü.
- `node --check` ile dosya sözdizimi doğrulandı; PosTerminal `corepack pnpm build` sıfır hata.

## Handoff

- None
