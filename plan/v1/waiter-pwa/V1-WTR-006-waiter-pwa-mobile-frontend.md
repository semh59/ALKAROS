# V1-WTR-006 - Waiter PWA mobile frontend implementation

- Task ID: V1-WTR-006
- Status: InProgress
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

Garsonların el terminali ve mobil cihazlarından masaları görüntülemesini, salon/bölge bazlı filtrelemesini, hızlı kategori/ürün/modifiyer seçimi ile sipariş oluşturmasını, offline kuyruk senkronizasyonunu ve mutfağa sipariş iletimini sağlayan mobil öncelikli (Mobile-First) Waiter PWA arayüzünü geliştirmek.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-006-waiter-pwa-mobile-frontend.md`
- `src/Clients/WaiterPwa/wwwroot/**`
- `tests/Clients/WaiterPwa/Frontend/**`
- `evidence/V1-WTR-006/**`

## In scope

- Mobil öncelikli (touch-friendly, WCAG 2.2 AA uyumlu, min 44x44px dokunma alanları) responsive PWA web arayüzü (`index.html`, `manifest.json`, `sw.js`, `waiter-app.js`, `waiter-app.css`).
- Garson PIN girişi ve oturum yönetimi.
- Masa listesi, bölge (Zone) filtreleme, masa durum rozetleri (`Available`, `Occupied`, `Reserved`, `Cleaning`) ve aktif sipariş özeti.
- Kategori sekmesi, hızlı ürün arama, ürün miktar yönetimi, modifiyer ve sipariş notu modalı.
- Çevrimdışı (Offline) işlem kuyruğu: ağ koptuğunda yerel kuyrukta tutma, çevrimiçi olunca otomatik idempotent gönderim ve durum göstergesi.
- Mutfak siparişi onaylama ve iletimi.
- Arayüz birim testleri ve doğrulama testleri.

## Out of scope

- Host API veya veritabanı şemasında geriye dönük uyumsuz değişiklik yapmak.
- Kredi kartı/mali POS donanım sürücüsü yazmak.

## Dependencies

- V1-WTR-005
- V1-RMD-036

## Acceptance evidence

- Waiter PWA statik dosyaları ve PWA manifesti eksiksiz yüklenir.
- Masalar listelenir, bölge filtresi çalışır, ürün ekleme ve sipariş gönderme akışı hatasız tamamlanır.
- Çevrimdışı modda siparişler kuyruğa alınır ve ağ geldiğinde mutfağa iletilir.
- Arayüz testleri ve plan audit doğrulayıcısı exit code `0` verir.

## Handoff

- V20-UAT-001
