# V1-RMD-738 - Billing doküman sapmaları ve KVKK runbook (603 B027, B039)

**Sonuç:** Billing'e dokunan altı doküman koda hizalandı, bir bayat kod yorumu düzeltildi. İşin doküman kısmını Haiku yaptı, her satırı ana oturum koda karşı doğruladı.

## Yapılanlar
- `projection-ownership.md`: `BillLineChanged` olayı kodda yok (grep src tests = 0); satır gerçeği yazıyor.
- `module-dependency-rules.md`: `BillStateChanged` yayımlanmıyor (grep = 0); "planned: no code publishes it yet" notu eklendi.
- `api-contract-standard.md`: Örnek 2'ye gerçek tender rotası, gövdede `IdempotencyKey` ve `*_KEY_REUSED` kodları notu.
- `remaining-gaps-register.md`: indirim/bahşiş istemcisi var; kalem bazlı bölme sunucuda var, Cashier'da `split-design` çağrısı yok (grep = 0); `PartiallyPaid` tanımlı ama hiçbir yerde atanmıyor (grep = 0).
- `migration-dependency-graph.md`: bill_allocations (020), bill_adjustments (021), payment_allocations (123), 032 bileşik FK ve 180 alanları eklendi.
- `kvkk-retention-runbook.md` Known gap: bill_items.notes, bill_adjustments.notes, bills.cancellation_reason, refund_requests.reason/decision_note anonimleşmiyor (`src/Modules/Privacy` içinde billing/refund referansı yok).
- `BillPaymentClosureModule.cs`: "DefaultCatalog'a eklenmedi" yorumu bayattı (modül `ModuleRegistry` satır 80'de kayıtlı); yorum düzeltildi, derleme temiz.

## Düzeltilen yanlış
Haiku register satır 22'de `customer-accounts.js` ve `void-comp.js` istemcilerini "özel hesap" karşılığı saymıştı; müşteri cari ekranı o uç nokta değil. Satır yalnız doğrulanan iddiaya indirildi.

## Yapılmayanlar
- `void-complimentary-discount-policy.md` satır bazlı/hesap seviyesi indirim çelişkisi: PO kararı (603 D5), dokunulmadı.
- Billing.csproj'a doğrudan Npgsql başvurusu: `packages.lock.json` bu görevin yazılabilir yüzeyinde değil, geri alındı; ayrı görevde.
- Çift DI kaydı ve kod yorumlarındaki görev kimlikleri: yalnız dokunulan dosyada temizlik yapıldı, geri kalanı yapılmadı.
- PO kararları B015, B026 ve dış bağımlılık B028 burada ele alınmadı.
