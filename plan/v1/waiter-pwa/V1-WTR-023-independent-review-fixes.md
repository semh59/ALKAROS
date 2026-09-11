# V1-WTR-023 - V1-WTR-012..022'nin bağımsız incelemesinde bulunanlar

- Task ID: V1-WTR-023
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatıyla ("Bu durak dur ve geriye dönük bağımsız bir incele",
2026-09-11): `garson-karsilastirma` ideation turunun tamamı (V1-WTR-012'den
V1-WTR-022'ye kadar, 10 commit) hiç bağımsız gözden geçirilmemişti — bu
oturumun kendi kodunu kendisi yazıp kendisi "Done" işaretlemesi dışında.
Dört paralel, bu koduyla hiç önceden teması olmayan ajan, birbirinden
bağımsız olarak her commit kümesini (V1-WTR-012-014, 015-017, 018-019,
020-022) düşmanca bir gözden geçirmeyle inceledi. Her bulgu, ajanın kendi
sözüne güvenmeden kodun ilgili satırları bizzat okunarak doğrulandı — üçü
gerçek çıktı, biri (TOCTOU yarışları, V1-012/014) önceden var olan, bu
oturumun miras aldığı bir mimari zayıflık olduğu için düzeltilmedi (ayrı
bir kapsam), biri de (test kapsam boşluğu, V1-WTR-018) doğrudan bir hata
değil, yalnızca eksik regresyon testiydi.

**1. HIGH — `/transfer-server` atomik değildi (V1-WTR-013'ün kendi
kodunda, bu oturumdan önce vardı, bu incelemede bulundu):**
`TransferServingUserAsync` (masaların gerçek devri) önce çalışıyor,
bağlam notunun 200 karakter sınırı doğrulaması sonra çalışıyordu. Not
sınırı aşarsa istek 400 dönüyordu ama masalar zaten devredilmişti —
istemciye söylenen sonuç, sunucudaki gerçek durumla uyuşmuyordu. Düzeltme:
`LeaveAsync` (notu doğrulayıp kaydeden, devirden bağımsız) artık ÖNCE
çağrılıyor; not reddedilirse hiçbir masa dokunulmamış kalıyor.

**2. Medium — koltuk değişikliği taslak yeniden gönderiminde sessizce
kaybolabiliyordu (V1-WTR-022, bu oturumda yazıldı):** `ItemContentUnchanged`
miktar/not/modifikatörleri karşılaştırıyordu ama `SeatId`'yi
karşılaştırmıyordu. Aynı miktar/not/modifikatörle yalnız koltuk değişse,
"değişiklik yok" sanılıp `ReconcileRound` hiç çağrılmıyordu — tam olarak
bu oturumun tekrar tekrar bulduğu "sessiz sıfırlama" kusur sınıfı, bu kez
elle yeniden kurulum değil bir eşitlik kontrolünde. Şu an WaiterPwa
arayüzünden tetiklenemiyor (var olan bir satırın koltuğunu değiştirme
arayüzü yok) ama gerçek bir açıktı.

**3. Medium/Low — bahşiş havuzu paydası tutarsızdı (V1-WTR-021, bu
oturumda yazıldı):** "kaç garson çalıştı" sorgusu `status <> 'Cancelled'`
filtresini atlıyordu; satış toplamı sorgusu atlamıyordu. İptal edilmiş
tek siparişi olan bir garson, hiç çalışmamış olmasına rağmen paydaya
giriyor, herkesin payını inceltiyordu — kod yorumunun "sales-total
sorgusuyla aynı popülasyon" iddiası doğru değildi.

**Ayrıca kapatıldı:** V1-WTR-018'in canlı adisyon uç noktasının en kritik
güvenlik korumasının (kapanmış/iptal sipariş → boş adisyon; iptal edilmiş
kalem → hiç görünmemeli) hiç regresyon testi yoktu — mantık doğruydu ama
korumasızdı. İki yeni test bu boşluğu kapatıyor.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-023-independent-review-fixes.md` (yeni)
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs,
    OrderManagementStore.cs (Host sahipliğinde) — `/transfer-server`'ın
    çağrı sırası, `ItemContentUnchanged`'e SeatId eklendi,
    `GetMyShiftSummaryAsync`'in payda sorgusuna `status <> 'Cancelled'`.
  - tests/Host/Experience/Orders/TableDraft/{OrderManagementTableDraftHttpTests.cs}
    (Host test sahipliğinde) — 2 yeni test.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (Billing test
    sahipliğinde) — 1 yeni test + `SetOrderStatusAsync` yardımcı
    fonksiyonu.
  - tests/Host/Experience/QrOrdering/{QrOrderingHttpTests.cs,
    QrOrderingTestDatabase.cs} (QrOrdering test sahipliğinde) — 2 yeni
    test, `SeedActiveOrderAsync`'e `status`/`itemStatus` parametreleri.

## Out of scope

- TOCTOU yarışları (kişisel ikram bütçesi tavanı, yardım-çağır bekleme
  süresi) — bu oturumdan önce var olan bir mimari desenin (check-then-
  insert, kilitleme yok) miras alınması; gerçek bir düzeltme (advisory
  lock veya serileştirilmiş bir transaction) daha büyük, ayrı bir görev.
  `CountAutoGrantsSinceAsync`'in de aynı deseni kullandığı doğrulandı —
  yeni bir kusur değil, var olan bir zayıflığın üçüncü tekrarı.

## Dependencies

- V1-WTR-013
- V1-WTR-018
- V1-WTR-021
- V1-WTR-022

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/Experience/Orders/TableDraft` → 63/63 (2 yeni test:
    aşırı uzun bir bağlam notu hiçbir masayı devretmeden reddedilir; aynı
    satırın yalnızca koltuğu değişse bile yeniden gönderim koltuğu
    günceller).
  - `tests/Host/Experience/Billing` → 18/18 (1 yeni test: iptal edilmiş
    tek siparişi olan bir garson bahşiş havuzu paydasını incelmez).
  - `tests/Host/Experience/QrOrdering` → 22/22 (2 yeni test: kapanmış bir
    sipariş, masanın current_order_id'si hâlâ ona işaret etse bile asla
    eski bir adisyon göstermez; iptal edilmiş bir kalem açık bir siparişin
    içinde bile asla görünmez).
  - `tests/Host/Experience/Orders/Comp` → 13/13 (regresyon kontrolü).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
