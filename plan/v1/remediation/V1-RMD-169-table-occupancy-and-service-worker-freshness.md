# V1-RMD-169 - Masa doluluğu para yerine durumdan, servis işçisi her zaman taze

- Task ID: V1-RMD-169
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümünden iki
Medium bulguyu kapatır ve "beş §0 ihlali" listesinin kalanını inceler.

1. **Masanın dolu olduğu paradan çıkarılıyor.** `renderTables()`,
   `table.amount > 0` ile "bu masada zaten sipariş var, hızlı ekle düğmesi
   göster" kararını veriyordu — sunucunun kendi `status` alanı zaten
   `'Occupied'` diyor olsa bile (V1-ORD-006: bir sipariş masaya bağlandığı
   an set edilir). Her satırı ikram edilmiş veya henüz fiyatlanmamış bir
   Draft turu olan bir masa yanlışlıkla "boş" görünüyordu. Artık
   `table.status === 'occupied'` — sunucunun kendi cevabı.
2. **`CACHE_NAME` elle güncellenmezse eski uygulama sonsuza kadar servis
   ediliyor.** Servis işçisinin kendisi (`sw.js`) değişmeden yalnız
   `waiter-app.js`/`waiter-app.css`/`index.html` değiştiğinde (CACHE_NAME
   bump'ı unutulduğunda — bu dosyalar bağımsız düzenleniyor, aralarında
   bağlayan bir derleme adımı yok), tarayıcı `install` olayını hiç
   tetiklemiyordu; eski önbellek sonsuza kadar servis ediliyordu. Uygulama
   kabuğu artık ağ-önce: çevrimiçi bir cihaz her zaman gerçek, güncel
   dosyayı alıyor; önbellek yalnızca amaçlandığı gibi çevrimdışı yedek
   olarak kalıyor (arka planda hâlâ güncelleniyor).

**"Beş §0 ihlali" listesinin kalan üçü incelendi, ikisinde kod değişikliği
gerekmedi:**

- **Devir hedefleri istemcide filtreleniyor** — `openTransferSheet()`,
  hedefleri `table.status === 'available'` ile filtreliyor.
  `PostgresTableTransferRepository.ExecuteTransferAsync`'in kendi kuralı
  incelendi: gerçek sunucu kısıtı BİREBİR aynı
  (`targetStatus != "Available"` → red). İstemci filtresi gerçek kuralla
  tam eşleşiyor, herhangi bir boşluk yok — §0.4'ün izin verdiği "yalnız
  UX, asla otoriter değil" durumu (sunucu zaten son sözü söylüyor).
  Düzeltme gerekmedi.
- **Eklenti adedi kuralı tekrar yazılıyor** — `modifierCountFor()`
  (`Math.max(1, Math.ceil(quantity))`), sunucunun aynı formülünü
  (`OrderManagementStore.BuildModifiers`'ın `defaultQuantity`'si)
  tekrarlıyor — ama yalnızca HENÜZ GÖNDERİLMEMİŞ turun önizleme
  toplamını göstermek için (kendi yorumu bunu zaten söylüyor: "the sent
  line always renders the server's own numbers"). Sunucu bu turu hiç
  görmediği için istemcinin BİR tahmin yapması kaçınılmaz — foundations.md'nin
  kendi header yorumu bunu "istemcinin tuttuğu tek yerel durum" olarak
  zaten tanımlıyor. Düzeltme gerekmedi.
- **Zorunlu seçim yalnız istemcide zorlanıyor** — V1-RMD-161 artık aynı
  kuralı sunucuda da (`ValidateModifierGroupSelections`) zorluyor;
  istemcideki kontrol (`onOptionsConfirm`'daki `minSelections` kontrolü,
  kendi yorumuyla "a UX check only") artık §0.4'ün sandığı, otoriter
  olmayan bir hızlı-geri-bildirim — otorite artık gerçekten sunucuda.
  Düzeltme gerekmedi (V1-RMD-161'in doğal bir yan etkisi).

(`canVoid`/`canVoidSent` beşinci madde, V1-RMD-168'de ayrı kapatıldı.)

## Owned surface

- `plan/v1/remediation/V1-RMD-169-table-occupancy-and-service-worker-freshness.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — `busy` artık `table.status`'tan.
  - src/Clients/WaiterPwa/wwwroot/sw.js (V1-WTR-006/V1-WTR-011
    sahipliğinde) — fetch handler ağ-önce'ye çevrildi.

## Out of scope

Frontend'in kalan bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-168

## Acceptance evidence

- `node --check` her iki JS dosyası için temiz.
- Sunucu tarafı iddiaların ikisi de gerçek kaynak koddan doğrulandı
  (`PostgresTableTransferRepository.cs:224` devir kuralı,
  `OrderManagementStore.cs`'in `BuildModifiers`'ı eklenti adedi kuralı) —
  varsayım yapılmadı.
- Bu görev için ayrı bir otomatik test eklenmedi (repoda bu iki dosya için
  JS test altyapısı yok, önceki istemci değişiklikleriyle aynı durum) —
  kod gözden geçirildi, `table.status` değerinin `loadTables()`'ta zaten
  küçük harfe çevrilip saklandığı (`(table.status ||
  'Available').toLowerCase()`) doğrulanarak `'occupied'` karşılaştırması
  doğru büyük/küçük harfle eşleştiği teyit edildi.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
