# V1-RMD-010 DOM/accessibility snapshots — 2026-08-27

Snapshots were taken from the authenticated real `PosTerminal` build at `https://localhost:58299` after the responsive
run. They are accessibility-tree snapshots, not fixture HTML.

## Cashier (`/`)

```text
- main:
  - generic: A
  - strong: ALKAROS
  - generic: Kasa satış
  - generic "Gerçek /health/ready sonucu": Sunucu hazır
  - button "Müşteri ekranı"
  - link "Masalar": /tables
  - link "Mutfak": /kitchen
  - link "Menü": /catalog
  - button "Ekranı eşleştir"
  - button "Oturumu kapat": Çıkış
  - generic "Sipariş özeti":
    - generic: Toplam
    - strong: ₺93,50
    - button "Sonraki sipariş"
  - navigation "Ürün kategorileri"
  - heading "Tüm ürünler" [level=1]
  - textbox "Ürün ara"
  - complementary:
    - text: AKTİF SİPARİŞ
    - heading "POS-20260827-053006-FA6523D64801" [level=2]
    - strong: "1"
    - strong: Espresso
    - generic: ₺93,50 × 1 · KDV dahil
    - generic: Toplam
    - strong: ₺93,50
    - strong: Sipariş gönderildi
```

## Tables (`/tables`)

```text
- banner:
  - generic "ALKAROS"
  - generic "Aktif çalışma bağlamı"
  - strong: Demo Kasiyer
  - strong: Kasiyer / Operasyon
- navigation "Ana navigasyon"
- main:
  - heading "Masa yönetimi" [level=1]
  - region "Masa yönetimi":
    - heading "Masa düzeni" [level=2]
    - button "+ Zone ekle"
    - button "+ Masa ekle"
    - button "Yenile"
    - combobox "Zone"
    - textbox "Masa ara"
    - group "Masa durumuna göre filtrele"
    - group "Görünüm"
    - generic "Masa özeti"
    - button "S-01 Dolu ... Müsait yap" [pressed]
    - button "S-02 Müsait ... Masayı aç"
    - complementary "S-01 masa bağlamı"
  - region "Masa sipariş bağlamı":
    - button "Siparişe devam et"
- contentinfo "Sistem durumu":
  - status: Çevrimiçi
```

## Catalog (`/catalog`)

```text
- main:
  - heading "Menü ve katalog" [level=1]
  - region "Menü ve katalog yönetimi":
    - heading "Menü ve katalog" [level=2]
    - button "+ Ürün ekle"
    - button "Yenile"
    - navigation "Catalog kaynak türü"
    - textbox "Catalog ara"
    - generic: Yayınlama yok · yalnızca manager CRUD
    - button "Espresso Aktif ESP-001 · MenuItem 85.00 TRY" [pressed]
    - complementary "Espresso ayrıntıları"
```

## Kitchen (`/kitchen`)

```text
- main:
  - heading "Mutfak ve operasyon" [level=1]
  - region "Mutfak ve operasyon yönetimi":
    - heading "hot-line istasyonu" [level=2]
    - generic: Kaynak: production API
    - button "Yenile"
    - generic "Mutfak özeti"
    - textbox "Ticket ara"
    - group "Ticket durumuna göre filtrele"
    - article:
      - generic: KT-POS-...-hot-line
      - generic: Bekliyor
      - button "→ Hazırlanıyor"
      - button "Kabul et"
      - button "İptal"
    - complementary "Mutfak operasyon uyarıları"
```

## Customer display (`/display`)

```text
- main:
  - generic: A
  - strong: ALKAROS
  - generic: GÜVENLİ EKRAN BAĞLANTISI
  - heading "Müşteri ekranını kasaya bağlayın" [level=1]
  - paragraph: Bu kodu kasa ekranındaki “Müşteri ekranı bağlantısı” alanına girin.
  - generic: <fresh eight-character code>
  - generic: Kod iki dakika sonra otomatik olarak yenilenir.
```

## Named pairing dialog

```text
- dialog "Ekran bağlantısını yönetin":
  - heading "Ekran bağlantısını yönetin" [level=2]
  - button "Eşleştirme penceresini kapat" [active]
  - textbox "Eşleştirme kodu"
  - button "Vazgeç"
  - button "Ekranı eşleştir" [disabled]
  - button "Aktif ekran yetkisini kaldır"
```
