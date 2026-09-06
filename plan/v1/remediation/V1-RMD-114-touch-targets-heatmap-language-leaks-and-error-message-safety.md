# V1-RMD-114 - Touch targets, heatmap thresholds, language leaks, and error-message safety

- Task ID: V1-RMD-114
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Dalga 3/N: `docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-06.md`'nin arayüz
katmanı bulgularının doğrulanmış, gerçek olanlarını kapatır.

## Owned surface

- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Clients/Cashier/wwwroot/cashier-app.css,
    src/Clients/WaiterPwa/wwwroot/waiter-app.css (ilgili istemci
    görevleri sahipliğinde) — .btn-micro/.btn-del/.item-actions eklendi
    (hiç tanımlı değildi), .btn-ticket-action/.btn-step 48px'e çıkarıldı.
  - src/Clients/PosTerminal/src/design-system/tokens.css,
    src/Clients/PosTerminal/src/shell/layout-contract.test.ts (ilgili
    görevler sahipliğinde) — --ds-target-min 44px'ten 48px'e; hiç
    kullanılmayan --ds-target-primary/--ds-target-touch kaldırıldı.
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx,
    tables.css, TableWorkspace.test.tsx (ilgili görev sahipliğinde) —
    ısı haritası eşiği 75/120 dk'dan DESIGN.md'nin 20/45 dk'sına;
    tokens.css'te zaten tanımlı ama hiç tüketilmeyen
    --ds-heat-fresh/-active/-stale renk token'ları artık kullanılıyor;
    15 "Zone" kullanıcı metni "Bölge" yapıldı; hata mesajı güvenliği
    (aşağıya bakın).
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (ilgili görev
    sahipliğinde) — "Revision" → "Sürüm"; hata mesajı güvenliği.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx,
    models.ts, src/Clients/PosTerminal/src/strings.ts,
    CatalogWorkspace.test.tsx (ilgili görev sahipliğinde) — "Menu item"/
    "Modifier"/"Add-on"/"Packaging"/"Service item" seçenek etiketleri,
    "MODIFIER ATAMALARI", "EFFECTIVE PRICE ZAMAN ÇİZELGESİ", "cashier
    görünürlüğü", "yönetici CRUD" Türkçeleştirildi; hata mesajı güvenliği.
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
    (ilgili görev sahipliğinde) — "Supervisor gerekli/yetkisi",
    "Reprint onaylandı/reddedildi/kararı", "worker", "<span>Order"
    Türkçeleştirildi; hata mesajı güvenliği.
  - src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.tsx,
    FloorPlanWorkspace.test.tsx, src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx,
    BillSplitWorkspace.test.tsx, src/Clients/PosTerminal/src/routes/workspace.tsx
    (ilgili görevler sahipliğinde) — hata mesajı güvenliği; workspace.tsx'te
    ayrıca bir "modifier" sızıntısı daha.
  - src/Modules/Recipes/CostSnapshots/IStockCostResolver.cs (Recipes'in
    kendi görevi sahipliğinde) — sessizce yutulan `42P01` (undefined_table)
    artık `LoggerMessage.Define` ile loglanıyor.

## In scope

1. **Dokunmatik hedef boyutu**: DESIGN.md'nin `--touch-target-min: 48px`
   spesifikasyonu — Cashier/WaiterPwa'da hiç tanımlanmamış
   (`.btn-micro`/`.btn-del`) veya küçük (`.btn-ticket-action` 32px,
   `.btn-step` 36px) butonlar; PosTerminal'in kendi `--ds-target-min`'i
   44px'ti. PosTerminal'de daha önce "kapsam ile çöz" denenmiş ama hiç
   uygulanmamış (`--ds-target-primary`/`--ds-target-touch` sıfır tüketici
   ile ölü kod olarak duruyordu) — spesifikasyona doğrudan eşitleyip ölü
   token'ları kaldırdım.
2. **Isı haritası eşiği**: `TableWorkspace.tsx`'te hardcoded 75/120 dakika,
   DESIGN.md'nin 20/45 kuralıyla uyuşmuyordu. `tokens.css`'te zaten doğru
   değerlerle (`--ds-heat-fresh-max-minutes: 20`, `-active-max-minutes: 45`)
   ve 3 renk token'ıyla (`--ds-heat-fresh/-active/-stale`) tanımlı ama hiç
   tüketilmeyen bir altyapı bulundu — renk token'larını
   `tables.css`'e bağladım (sayısal eşikleri JS sabiti olarak tuttum,
   CSS'ten JS'e okuma bu kod tabanında hiç kullanılmayan yeni bir desen
   olurdu).
3. **Dil sızıntıları**: "Zone" (15 yer, → Bölge), "Revision" (→ Sürüm),
   "Menu item"/"Modifier"/"Add-on"/"Packaging"/"Service item"/"MODIFIER
   ATAMALARI"/"EFFECTIVE PRICE ZAMAN ÇİZELGESİ"/"cashier"/"CRUD",
   "Supervisor"/"Reprint"/"worker"/"Order".
4. **Ham hata mesajı güvenliği**: `reason instanceof Error ? reason.message
   : ...` deseni 8 PosTerminal dosyasında 21 yerde — bir `ApiError`
   (sunucunun kendi Türkçe eşlemesinden gelir) ile GERÇEK bir ağ hatasını
   (`fetch()`'in kendisi patlarsa, tarayıcının kendi İngilizce mesajı,
   örn. "Failed to fetch") ayırt etmiyordu. Artık yalnızca `ApiError`
   güvenilir kabul ediliyor.
5. `IStockCostResolver`'ın sessiz `42P01` yutması artık loglanıyor
   (davranış aynı kalıyor — null dönüyor — ama artık görünür).

## Out of scope

- Isı haritasının "3 renkli durum halkası" olarak (edilgen renkli metin
  yerine gerçek bir halka/rozet şekli) görselleştirilmesi — bu bir şekil/
  bileşen tasarımı değişikliği, token/eşik düzeltmesi değil; ayrı bir
  karar.
- Bearer token tutarsızlığı — doğruladım ama gerçek bir sızıntı yok:
  WaiterPwa'nın Bearer başlığı yalnızca zaten Bearer destekleyen
  `table-draft`/`submit-draft`'a gönderiliyor (`postOrderToBackend`),
  her zaman cookie ile birlikte. Diğer 5 modüle Bearer desteği eklemek,
  bugün onu hiç kullanmayan bir çağıran olmadan, tam da yasaklanan
  "hayali kod" olurdu.
- PIN kaba kuvvet koruması, koltuk/aşama bazlı sipariş — bunlar "düzeltme"
  değil, yeni özellik — ayrı bir kapsam kararı gerekiyor.
- WaiterPwa'nın IndexedDB/UUIDv7'ye geçişi — mevcut localStorage/UUIDv4
  doğru çalışıyor (gerçek bir defekt değil), yeniden yazım riski
  kazanımından büyük; ayrı bir karar.
- Hata zarfı birleştirme (9 farklı şekil) — repo genelinde büyük bir
  refactor, bu dalganın kapsamına orantısız; not edildi, dokunulmadı.

## Dependencies

- V1-RMD-113

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --rm test`:
  **79/79 test projesi, sıfır başarısız** (`ALKAROS.Recipes.CostSnapshots.Tests`
  12/12 dahil).
- `npx vitest run` (PosTerminal): **119/119**, `npx tsc --noEmit`: temiz.
- Revert-and-confirm: (1) ısı haritası eşiği geçici olarak 75/120'ye
  döndürülüp 2 test (warn/crit) beklendiği gibi başarısız oldu, geri
  yüklenip yeşil; (2) `TableWorkspace.tsx`'in hata mesajı güvenliği
  geçici olarak eski (güvensiz) haline döndürülüp gerçek bir
  `new Error("Failed to fetch")` enjekte edildi — DOM'da ham "Failed to
  fetch" metni göründüğü doğrulandı, kod geri yüklenip metin bir daha
  hiç görünmedi.
- `python tools/plan-audit/plan_audit_tool.py validate`,
  `validate-coverage`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: sıfır ihlal (bu
  görevin kendi bir yorumundaki gerçek ama Türkçe karakter içeren bir
  alıntı önce kendi ihlalini üretti, İngilizceleştirilip düzeltildi; depo
  genelinde 13 ihlal öncekiyle aynı, hepsi bu görevin dokunmadığı
  dosyalarda).

## Handoff

- V1-GOV-105
