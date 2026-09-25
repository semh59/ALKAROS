# V1-RMD-279 - Kasa kuyruğu hesabı izler: ödenen hesap kuyruktan çıkar, kısmi ödeme görünür

- Task ID: V1-RMD-279
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-ORD-006` (`docs/design/modules/check-and-table.md`) kilitli kararı: garson "Hesabı kasaya gönder"
der, hesap masadan KOPAR, masa hemen yeni müşteriye açılır, hesap kasanın "ödeme bekleyen hesaplar"
kuyruğunda durur. Ödeme o gün kapsam dışıydı ve tasarım açıkça şunu kabul etmişti: "kuyruk kendiliğinden
boşalmaz". Ödeme artık var (Faz 2, `V1-RMD-276`), ama kuyruk hâlâ yalnız `orders.status = 'Submitted'` ve
"masaya bağlı değil" diye süzüyordu: ödenen hesap kuyrukta sonsuza dek kalıyordu.

`GET /orders/awaiting-payment` artık hesabı izler: hesabı `Paid` olan sipariş kuyruktan çıkar (sipariş
durumu `Submitted` kalır; sipariş yaşam döngüsü ayrı, daha büyük bir eksik); satır hesabın kimliğini
(`billId`, kasiyer henüz hesap açmadıysa boş) ve o hesaba tahsis edilmiş tutarı (`paidAmount`) taşır ki
kasiyer kısmen ödenmiş hesaba kaldığı yerden devam etsin. Masa hiçbir noktada değişmez.

Ayrıca `TableDraft` test fixture'ında eksik olan migrasyonlar (`118` stok kalemi yeniden sipariş noktası,
faturalama, ödemeler, tahsisler) eklendi: bu proje temiz ağaçta bile 6 testte `503` veriyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-279-till-queue-follows-the-bill.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/CashierHandoffStore.cs
  (V1-ORD-006 sahipliğinde kalır — yalnız kuyruk sorgusu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementContracts.cs
  (aynı sahiplikte — yalnız `PendingCheckSummaryV1`'e iki isteğe bağlı alan)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs
  (aynı sahiplikte — 1 yeni test)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftTestDatabase.cs
  (yalnız hesap/tahsis tohumlama yardımcıları)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj
  (yalnız eksik fixture migrasyonları)

## In scope

1. Kuyruk sorgusu, iki yeni alan, gerçek Postgres testi, fixture düzeltmesi.

## Out of scope

- Kasiyer kuyruk ekranı (V1-RMD-280).
- Sipariş yaşam döngüsünün (`Served`/`Completed`) çağrılması.
- Masa durumu: bu görevde ve kilitli tasarımda ödeme masaya dokunmaz.

## Dependencies

- V1-ORD-006
- V1-RMD-276

## Acceptance evidence

- Host.Experience.Orders.TableDraft (UTF8 Postgres 18): 80/80. Yeni test: kasaya gönderilen hesap kuyrukta,
  `billId` boş; kasiyer hesap açınca `billId` dolu; 100 ₺ tahsisle `paidAmount = 100`; hesap `Paid` olunca
  kuyruktan çıkar. Önceden kırık 6 test fixture düzeltmesiyle geçti.
- `python tools/consistency-audit/consistency_audit.py` ve `python tools/plan-audit/plan_audit_tool.py validate` temiz.

## Handoff

- None
