# qa-dokuman-bagimlilik - V1-RMD-600 (SHA e00a61c0)

Kapsam: 71 kalem sınıflandırıldı (17 migration yol/görev eşleşmesi, SQL yalnız 006/084/053 okundu; 14 Catalog kaynağı; 4 packages.lock.json + 4 csproj + Directory.Packages.props; 12 istemci/E2E/test yalnız yol düzeyi). Derleme/test koşulmadı.

| No | Önem | Kanıt | Sorun |
|---|---|---|---|
| D-001 | Orta | NfcOrderingStore.cs:348, KitchenOrderSubmissionDispatcher.cs:182 | is_age_restricted yalnız okunuyor; Catalog'da yazan yol yok; V12-NFC-002 Owned surface "Catalog read/write contract" yazıyor ama yapılmamış (bilinen: mevzuat M-004, güvenlik G-003). |
| D-002 | Orta | docs/compliance/money-tax-business-date.md:52, 006-catalog.up.sql:19 | Doküman sapması: tax_code 1/10/20 ve line_type bazlı seçim dokümanda; kodda tax_profiles.code serbest VARCHAR(50), vat_rate 0-100, tohum yok, tax_code sütunu/line_type bağı yok (`grep tax_code src database` → 0). Kod esas, karar verilmedi. |
| D-003 | Orta | docs/architecture/api-contract-standard.md:38-39, CatalogManagementEndpoints.cs:55-182, CatalogManagementStore.cs:115,130 | Standart tüm yazma uçlarında X-Idempotency-Key ve güncellemelerde X-Row-Version istiyor; 9 yazma ucu ikisini de kullanmıyor; sürüm sunucuda taze okunup geri veriliyor, iyimser eşzamanlılık yalnız okuma-yazma yarışında koruma. Idempotency sapması bilinen (skill listeliyor); X-Row-Version sapması listede yok. |
| D-004 | Düşük | docs/architecture/module-dependency-rules.md:47 | ProductCatalogChanged olayı belgelenmiş, kodda yok (`grep ProductCatalogChanged src` → 0) (bilinen: sınır S-002). |
| D-005 | Düşük | plan/v1/catalog/V1-CAT-003-...md, plan/TASK_STANDARD.md:107-110 | V1-CAT-003 NotApplicable ama Owned surface'ta evidence/V1-CAT-003/** var, klasör yok. |
| D-006 | Düşük | evidence/, TASK_STANDARD.md:107-110 | Kanıt klasörü yok: V12-NFC-002, V1-RMD-103, V1-RMD-156, V1-WTR-017 (kural 2026-09-28 sonrası; tarihsel, bilgi); V1-CAT-001/002 yalnız closure-evidence.txt. |
| D-007 | Düşük | tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj:14 | Npgsql PackageReference var, dizindeki .cs dosyalarında kullanılmıyor (gereksiz doğrudan referans). |
| D-008 | Düşük | docs/engineering/third-party-license-policy.md | Catalog'a özgü lisans bulgusu yok; transitif paketler için makine okunur lisans envanteri yok (politika söylüyor, V1-RMD-596 bilinen). |

Temiz: Done görevlerin (V1-CAT-001/002, V1-RMD-103/156, V1-WTR-017, V1-GOV-040) Owned surface yolları mevcut; V1-CAT-001 kabul koşulları test edilmiş (tekil kod, enum, çakışma testi); Enums.cs ↔ canonical-value-catalog.md:72-73 birebir; Catalog csproj'ları yalnız Directory.Packages.props sürümleri (Npgsql 9.0.2, FluentAssertions 6.12.0), 4 lock dosyası; PosTerminal node_modules 258 paket (MIT, Apache-2.0, ISC, BSD, MIT-0, Unlicense, 5 MPL-2.0 — adları doğrulanmadı), GPL/AGPL/SSPL/lisanssız yok; E2E lock'ları MIT/Apache/ISC; kaynak/test/istemcide TODO/NotImplemented/Skip yok.
Doğrulanamayan: transitif paket lisansları; docs/data/* (migration-dependency-graph, projection-ownership, nullable-unique-policy) ve docs/domain/* Catalog atıfları satır satır karşılaştırılmadı; 097 kısıtlarının doküman uyumu; selection_type satırı açılmadı.
