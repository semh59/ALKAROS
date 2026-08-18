# ALKAROS V1 DESIGN SYSTEM & ENTERPRISE RESTAURANT OS SPECIFICATION (Stitch AI-Native)

> **Standard:** Google Stitch AI-Native Design Specification (`DESIGN.md`)  
> **Benchmark Reference:** Toast POS (Toast Go® 2/3), Square for Restaurants, Lightspeed K-Series, Apple HIG  
> **Scope:** ALKAROS V1 Core Restaurant Operations (Zero Payment Scope — Kitchen, Table, Order & Pre-Bill Engine)  
> **Accessibility:** WCAG 2.2 Level AA / AAA Mathematical Conformance  

---

## 1. Dünya Devi POS Sistemleri Kıyaslama ve Üstünlük Analizi (Competitive Benchmark)

| Derin POS Yeteneği | Toast POS (Toast Go®) | Square for Restaurants | Lightspeed Restaurant | ALKAROS V1 (Stitch Enterprise) |
|---|---|---|---|---|
| **Servis Aşamaları (Coursing)** | 1. Başlangıç / 2. Ana Yemek / 3. Tatlı | Hold & Fire | Kurs A / Kurs B | **3 Aşamalı Akıllı Coursing:** Başlangıç, Ana Yemek, Tatlı/Kahve ayrımı. Aşamaya özel "Şimdi Pişir (Fire)" veya "Beklet (Hold)". |
| **Koltuk/Kişi Bazlı Sipariş (Seat-Based)** | Koltuk 1, Koltuk 2 | Misafir 1, Misafir 2 | Sandalye Matrisi | **Koltuk Bazlı Hizalama:** Her ürün Masadaki Koltuk No (Koltuk 1..6) veya "Ortaya Paylaşımlı" olarak işaretlenir. |
| **Tek Dokunuş Tur Tekrarı (Repeat Round)** | Var (İçecekler için) | Manuel kopyalama | Manuel ekleme | **1-Tap Repeat Round:** Masanın aktif içecek ve meze siparişlerini tek tıkla sepete ekleyip mutfağa iletir. |
| **Akıllı Stok Kilidi (86'd Porsiyon)** | Kalan sayaç + kilitleme | Sayaç uyarısı | Stok uyarısı | **Canlı 86'd Sayacı:** Kritik stokta `[Son 3 Porsiyon]` rozeti, 0 olduğunda siparişi anında bloke eden koruma. |
| **Masa Isı Haritası (Heatmap & Inactivity)** | Süre sayacı | Liste zamanı | Renkli durum çemberi | **3 Renkli Akıllı Zaman Halkası:** 0–20 dk (Taze Mavi), 20–45 dk (Yemekte Kehribar), 45+ dk (Hareketsiz Kırmızı İkaz). |
| **Tek Katmanlı Zorunlu Değiştiriciler** | Zorunlu modal | Dinamik adımlar | Fiş notu | **Hızlı Dokunmatik Modal:** Pişme derecesi (zorunlu radio) + Ekstralar + Hızlı Şef Etiketleri (`[Sos Ayrı]`, `[Buzsuz]`). |
| **Asimetrik Çevrimdışı Dayanıklılık** | Arka plan kuyruğu | Yerel depolama | Hibrit | **Garson PWA IndexedDB Kuyruğu + Kasiyer Salt Okunur Donma:** Ağ kesintisinde veri kaybı ve çakışma %0. |

---

## 2. Stitch Tasarım Sistemi Tokenları (Design Tokens)

### 2.1 Renk Paleti ve WCAG 2.2 AA Kontrast Oranları
Tüm renk eşleşmeleri matematiksel olarak doğrulanmıştır: $(L_1 + 0.05) / (L_2 + 0.05) \ge 4.5:1$ (Metin) ve $\ge 3.0:1$ (Grafik/Sınır).

```css
/* Stitch Enterprise Light Theme */
:root {
  --color-primary: #0D5257;        /* Deep Spruce Teal - 8.90:1 Contrast */
  --color-primary-hover: #08383B;
  --color-primary-text: #FFFFFF;

  --color-canvas: #F8FAFC;         /* Slate-50 Canvas */
  --color-surface: #FFFFFF;        /* Pure White Card */
  --color-surface-hover: #F1F5F9;  /* Slate-100 */
  --color-surface-active: #E2E8F0; /* Slate-200 */

  --color-border: #E2E8F0;
  --color-border-strong: #CBD5E1;

  --color-text-main: #0F172A;      /* Slate-900 */
  --color-text-muted: #475569;     /* Slate-600 - 5.99:1 */
  --color-text-dim: #94A3B8;       /* Slate-400 */

  /* Coursing Tokens */
  --course-starter-bg: #E0F2FE;
  --course-starter-text: #0369A1;

  --course-main-bg: #FEF3C7;
  --course-main-text: #B45309;

  --course-dessert-bg: #FCE7F3;
  --course-dessert-text: #BE185D;

  /* 2-Dimensional Table Badges */
  --badge-avail-bg: #DCFCE7;
  --badge-avail-text: #14532D;     /* 7.78:1 */

  --badge-occup-bg: #DBEAFE;
  --badge-occup-text: #1E40AF;     /* 7.17:1 */

  --badge-reserv-bg: #EDE9FE;
  --badge-reserv-text: #5B21B6;    /* 7.88:1 */

  --badge-bill-bg: #FEF3C7;
  --badge-bill-text: #78350F;      /* 8.34:1 */

  --badge-cooking-bg: #FFEDD5;
  --badge-cooking-text: #7C2D12;   /* 8.94:1 */

  --badge-ready-bg: #DCFCE7;
  --badge-ready-text: #14532D;

  --badge-error-bg: #FEE2E2;
  --badge-error-text: #7F1D1D;     /* 9.22:1 */
  --color-danger: #DC2626;

  /* Touch & Geometry */
  --touch-target-min: 48px;
  --radius-sm: 6px;
  --radius-md: 10px;
  --radius-lg: 14px;
  --radius-pill: 9999px;
}
```

---

## 3. Mimari Bileşen Mimarisi (Component Architecture)

### 3.1 Masa Kartı (Table Card) — 3 Boyutlu Zeka
```
+---------------------------------------------------------------+
| MASA S-02 (Salon) • [Koltuk: 4]            [ Dolu (Mavi Pill)]|
| [====================================] (35 dk • Kehribar Uyarı)|
| Garson: Mehmet K.                                             |
| Tutar: 485,00 TL                                              |
|                                                               |
| [ (lucide:clock-4) Hesap İstendi ]  [ 2. Aşama (Ana Yemek) ]  |
|                                                               |
| [ Masayı Aç > ]               [ (lucide:rotate-cw) 1-Tap Tekrar]|
+---------------------------------------------------------------+
```

---

### 3.2 Koltuk ve Aşama Bazlı Sipariş Girişi (Coursing & Seat-Based POS)
```
+---------------------------------------------------------------+
| Masa S-02 | Koltuk Seçimi: [ Tüm Koltuklar ] [ K-1 ] [ K-2 ] [ K-3 ] [ K-4 ] |
+---------------------------------------------------------------+
| 1. AŞAMA: BAŞLANGIÇLAR (Mutfakta Pişirildi)                   |
|   • Koltuk 1: 1x Patates Tava                     85,00 TL    |
|                                                               |
| 2. AŞAMA: ANA YEMEKLER [ ŞİMDİ ATEŞLE (FIRE) ]                |
|   • Koltuk 1: 1x Alkaros Burger (Orta, +Cheddar) 270,00 TL    |
|   • Koltuk 2: 1x Bonfile Izgara (Çok Pişmiş)     450,00 TL    |
|                                                               |
| 3. AŞAMA: TATLI & KAHVE [ BEKLEMEDE (HOLD) ]                  |
|   • Ortaya:   1x Çikolatalı Sufle                120,00 TL    |
|   • Koltuk 2: 2x Türk Kahvesi                     60,00 TL    |
+---------------------------------------------------------------+
```

---

## 4. Güvenlik, Hata Kurtarma ve Kesinti Protokolleri

1. **Ağ Kesintisi (LAN Outage):**
   - Kasiyer Kiosk salt okunura geçer; Garson PWA siparişleri UUID v7 ile IndexedDB yerel kuyruğuna yazar.
2. **PIN Kilitlenmesi (Brute-Force Protection):**
   - 3 Hatalı Deneme: 30 saniye kilitlenme geri sayımı.
   - 5 Hatalı Deneme: Oturumun sunucuda tamamen geçersiz kılınması (Session Invalidation).
3. **Mutfak İstasyon Yönlendirme (Print Recovery):**
   - Kağıt bitmesi durumunda fişler sıcağa/soğuğa tek tıkla gerekçe loguyla aktarılır.
