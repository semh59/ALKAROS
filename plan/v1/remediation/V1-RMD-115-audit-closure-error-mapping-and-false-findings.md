# V1-RMD-115 - Audit closure: error mapping consistency and false findings

- Task ID: V1-RMD-115
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Dalga 4/N (son dalga): `docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-06.md`'nin
geri kalan bulgularının her birini tek tek doğrular; gerçek olanı düzeltir,
yanlış/abartılı olanı gerekçesiyle kapatır. Bu, raporun V1 kapsamındaki
tüm maddelerinin (Katman 1-4) tüketildiği son dalgadır.

## Owned surface

- `plan/v1/remediation/V1-RMD-115-audit-closure-error-mapping-and-false-findings.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/DualScreen/DualScreenApplication.cs, src/Host/Experience/Billing/BillingSplitApplication.cs,
    src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs,
    src/Host/Experience/Orders/OrderManagementEndpoints.cs,
    src/Host/Experience/Tables/TableManagementApplication.cs (ilgili görevler
    sahipliğinde) — her birinin `Map()`/`WriteErrorAsync` switch'ine
    `BadHttpRequestException` eklendi (400 VALIDATION_FAILED), Catalog/
    Kitchen/Roles/Authorization'ın zaten sahip olduğu desenle tutarlı hale
    getirildi.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-BIL-005
    sahipliğinde) — yeni bir regresyon testi + doğrulama sırasında bulunan
    gerçek durumu belgeleyen yorum.

## In scope

1. **Bulgu F (400 yerine 500) — kısmen doğrulandı, kısmen yanlış.**
   Denetim iddiası: "5 modülün hata filtrelerinde `BadHttpRequestException`
   yakalanmamakta, istemci hatası 500 gibi görünmektedir." Doğrulama: 9
   Host/Experience modülünden 4'ü (`Catalog`, `Kitchen`, `Roles`,
   `Authorization`) bu case'i ZATEN önceki bir dalgada eklemişti; 5'i
   (`DualScreenApplication` — terminal-geneli ana hata işleyici, `Billing`,
   `OfflineReconciliation`, `Orders`, `Tables`) eksikti — bu kısım gerçek,
   düzeltildi.
   **Ancak** gerçek bir HTTP testiyle (`BillingSplitHttpTests.
   MalformedJsonBodyReturns400WithoutReachingTheEndpointFilter`) doğrulandı ki
   iddianın kendisi (ham/bozuk JSON gövdesi 500 üretir) YANLIŞ: ASP.NET
   Core'un minimal-API JSON gövde bağlayıcısı bir ayrıştırma hatasını
   kendi içinde yakalayıp boş gövdeli bir 400 yazıyor — istek delegesi
   (ve dolayısıyla hiçbir `IEndpointFilter`) hiç çalışmıyor. Revert-and-confirm:
   `BillingSplitApplication.Map()`'teki `BadHttpRequestException` case'i
   geçici kaldırılıp aynı test tekrar çalıştırıldı, sonuç DEĞİŞMEDİ (hâlâ
   400). Eklenen case, bu senaryoyu değil, henüz tespit edilmemiş başka bir
   `BadHttpRequestException` kaynağını (ör. bir handler içinde elle
   fırlatılan) kapsayan savunma amaçlı bir tutarlılık düzeltmesi olarak
   kalıyor — canlı bir 500 kusurunu düzelttiği KANITLANMADI.
2. **Bulgu (Katman 1.A, direkt proje referansları) — daha önce yanlış
   bulunmuştu** (`Kitchen->Orders`, `Billing->Orders` `ModuleBoundaryTests.
   ApprovedEdges`'in kendi onaylı, test edilen kenarları; `V1-RMD-114`'ün
   ait olduğu dalgada zaten doğrulanmıştı). `Inventory->Recipes` gerçekti,
   `V11-RMD-002` ile kapatıldı.
3. **Bulgu (Katman 4.C, 22 cross-schema FK) — yanlış/abartılı.**
   `docs/architecture/module-dependency-rules.md`'nin kendi karar kaydı
   PDF:I.1.1'in "Single application/backend deployment", "Single PostgreSQL
   instance" modelini temel alır — modüller AYNI fiziksel veritabanını
   paylaşır, yalnızca ayrı şemalara sahiptir. Kısıtlanan şey bir modülün
   KODUNUN başka bir modülün şemasına YAZMASI (rule 5, `tools/consistency-audit`),
   bir referans bütünlüğü FK KISITI değil — bir FK, hangi modülün kodunun
   INSERT/UPDATE yaptığını değiştirmez, yalnızca Postgres'in kendisinin
   referans bütünlüğünü zorlamasını sağlar. V0-ARC-001'in metninde
   "cross-schema FK yasaktır" diye bir kural yoktur. Döngüsel FK
   (`table_mgmt.tables.current_order_id` ↔ `orders.orders`) zaten ayrı
   olarak doğrulanıp (`011-orders.up.sql`'deki `TRACEABILITY C50` yorumu)
   kasıtlı bir tasarım kararı olduğu teyit edilmişti; kalan 21 FK aynı
   paylaşılan-veritabanı modelinin normal, öngörülen bir sonucu.
4. **Bulgu (Katman 3.A, Bearer token) — daha önce yanlış bulunmuştu**
   (`V1-RMD-114`'ün ait olduğu dalgada: WaiterPwa'nın TEK Bearer gönderdiği
   çağrı zaten Bearer destekleyen `table-draft`/`submit-draft`'a gidiyor,
   her zaman cookie ile birlikte; gerçek bir sızıntı yok).

## Out of scope

- **PIN kaba kuvvet koruması** (Katman 2.B) — hiçbir arayüzde PIN klavyesi
  bile yok; eklemek yeni bir özellik, düzeltme değil (`V1-RMD-114`'te de
  aynı gerekçeyle kapsam dışı bırakılmıştı).
- **Coursing (Hold/Fire) ve koltuk bazlı sipariş** (Katman 2.C) — yeni
  özellik, ayrı bir kapsam kararı gerektiriyor.
- **Isı haritasının 3 renkli durum halkası olarak görselleştirilmesi**
  (Katman 2.C) — eşik değerleri (`V1-RMD-114`) düzeltildi; şekil/bileşen
  tasarımı ayrı bir karar.
- **WaiterPwa'nın IndexedDB/UUIDv7'ye geçişi** (Katman 2.A) — mevcut
  `localStorage`/UUIDv4 doğru çalışıyor, gerçek bir defekt değil; yeniden
  yazım riski kazanımından büyük.
- **Kiosk çevrimdışıyken kasiyer girişinin kilitlenmesi** (Katman 2.A,
  `Cashier.tsx`) — bu davranış kasıtlı bir güvenlik/tutarlılık tercihi
  olabilir (çevrimdışı bir terminalde kimlik doğrulaması yapılamaz); bir
  defekt olarak ele almak için önce beklenen davranışın ne olması
  gerektiğine dair bir ürün kararı gerekiyor — düzeltme değil.
- **9 farklı hata zarfı birleştirme** (Katman 3.G) — repo genelinde büyük
  bir refactor, `V1-RMD-114`'te de aynı gerekçeyle kapsam dışı bırakılmıştı.
- **Outbox/Inbox retansiyon ve temizlik mekanizması** (Katman 4.F) — yeni
  bir operasyonel yetenek (saklama süresi politikası, arşivleme kararı
  gerektiriyor), bugün hiçbir kurulumda milyonlarca satıra ulaşılmadı;
  düzeltme değil, ayrı bir karar.

## Dependencies

- V1-RMD-114
- V11-RMD-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `dotnet test` (etkilenen 7 proje — Billing, OfflineReconciliation, Tables,
  Orders/TableDraft, Orders/Comp, Orders/Void, Orders/VoidSent): tümü yeşil,
  yeni `MalformedJsonBodyReturns400WithoutReachingTheEndpointFilter` dahil.
- Revert-and-confirm: `BillingSplitApplication.Map()`'teki
  `BadHttpRequestException` case'i geçici kaldırılıp aynı test tekrar
  çalıştırıldı — sonuç DEĞİŞMEDİ (bulgunun bu senaryo için yanlış olduğunun
  kanıtı); case geri yüklenip normale döndü.
- `python tools/plan-audit/plan_audit_tool.py validate`,
  `validate-coverage`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi
  bu görevden önce de vardı, dokunulmayan dosyalarda.
- Bu görevle birlikte `docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-06.md`'nin
  4 katmanının tamamındaki her madde ya düzeltildi (bu dalga + `V1-RMD-112`,
  `113`, `114`, `V11-RMD-001`, `V11-RMD-002`) ya da yanlış/abartılı olduğu
  kanıtlanarak kapatıldı ya da yeni özellik olarak ayrı bir karara
  bırakıldı — raporda açık, doğrulanmamış bir madde kalmadı.

## Handoff

- V1-GOV-107
