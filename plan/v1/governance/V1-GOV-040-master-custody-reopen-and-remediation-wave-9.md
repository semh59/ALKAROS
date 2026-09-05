# V1-GOV-040 - Master custody reopen and remediation wave 9

- Task ID: V1-GOV-040
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

2026-08-31 tarihli derin V1 incelemesinden kalan yapılandırılabilir özellik ve yerleşim eksikliklerini kapatmak için `GATE-V1-EXIT` kapısını yeniden açmak: mutfak bileti hedef hazırlık süresinin domain sözleşmesine alınması, katalog ürün kullanılabilirliği ("86") toggle özelliği, mutfak sağlık panelinin ayrı yönetici rotasına taşınması, Cashier terminal kimliğinin cihaz oturumundan türetilmesi ve masa metadata alanlarında alan bazlı çakışma birleştirmesi. 5 kurtarma görevi (`V1-RMD-074..078`) ve kapanış görevi (`V1-GOV-041`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-040-master-custody-reopen-and-remediation-wave-9.md`
- `plan/v1/remediation/V1-RMD-074-kitchen-ticket-target-prep-time-contract.md`
- `plan/v1/remediation/V1-RMD-075-posterminal-kitchen-and-system-health-workspace.md`
- `plan/v1/remediation/V1-RMD-076-catalog-product-availability-suspension.md`
- `plan/v1/remediation/V1-RMD-077-cashier-terminal-session-provisioning.md`
- `plan/v1/remediation/V1-RMD-078-table-metadata-field-level-merge.md`
- `plan/v1/governance/V1-GOV-041-wave9-master-audit-reseal-and-gate-closure.md`
- `plan/v1/remediation/V1-RMD-001-consolidated-remediation.md`
- `plan/v1/remediation/V1-RMD-063-catalog-price-schema-alignment.md`
- `plan/v1/remediation/V1-RMD-065-kitchen-ticket-idempotency.md`
- `plan/v1/remediation/V1-RMD-069-kitchen-workspace-turkish-remediation-and-health-relocation.md`
- `plan/v1/remediation/V1-RMD-070-catalog-workspace-turkish-string-remediation.md`
- `plan/v1/remediation/V1-RMD-073-cashier-fail-closed-catalog-and-session-identity.md`
- `plan/v1/remediation/V1-RMD-058-posterminal-billing-state-synchronization.md`
- `plan/v1/remediation/V1-RMD-054-production-composition-and-order-billing-routing.md`
- `plan/v1/remediation/V1-RMD-050-order-endpoint-and-dualscreen-unification.md`
- `plan/v1/remediation/V1-RMD-035-deep-code-audit-remediation.md`
- `plan/v1/remediation/V1-RMD-026-floor-plan-persistence-and-api.md`
- PO:2026-09-01 kararıyla database/MigrationComposition/order.json yüzeyi V1-RMD-089'a devredildi; bu historical task closed kalır ve migration composition 16. dalgada güncellenir.
- `database/migrations/V1/V1-GOV-040/**`
- PO:2026-09-01 kararıyla tests/Host/MigrationComposition/Manifest/ManifestTests.cs yüzeyi V1-RMD-089'a devredildi; bu historical task closed kalır ve manifest testleri 16. dalgada güncellenir.
- `plan/GATES.md`
- `plan/v1/README.md`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 9. dalga görevleriyle (`V1-RMD-074..078`, `V1-GOV-041`) genişletmek ve sayımı güncellemek.
- 5 kurtarma görevi ile 1 kapanış görevini tanımlamak; her biri tek sahipli ve tek yüzeyli olur.
- Tamamlanmış görevlerden yüzey devirleri: `src/Modules/Kitchen/TicketLifecycle` ve `src/Host/Experience/KitchenOperations` ve bunların test dizinleri V1-RMD-065'ten V1-RMD-074'e; `src/Clients/PosTerminal/src/features/kitchen-operations` V1-RMD-069'dan V1-RMD-075'e; `src/Clients/PosTerminal/src/App.tsx` V1-RMD-058'den V1-RMD-075'e; `src/Modules/Catalog/ProductCatalog/Product.cs`, `src/Modules/Catalog/ProductCatalog/PostgresProductRepository.cs` ve ilgili test dosyaları V1-RMD-001'den V1-RMD-076'ya; `src/Host/Experience/Catalog` ve test dizini V1-RMD-063'ten V1-RMD-076'ya; `src/Clients/PosTerminal/src/features/catalog` V1-RMD-070'ten V1-RMD-076'ya; `src/Host/DualScreen/DualScreenApplication.cs` V1-RMD-054'ten V1-RMD-077'ye; `src/Host/DualScreen/DualScreenStore.cs` V1-RMD-050'den V1-RMD-077'ye; `tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs` V1-RMD-035'ten V1-RMD-077'ye; `src/Clients/Cashier/wwwroot` V1-RMD-073'ten V1-RMD-077'ye.
- `database/MigrationComposition/order.json` ve `tests/Host/MigrationComposition/Manifest/ManifestTests.cs` yüzeylerini V1-RMD-026'dan devralmak; katalog ürünü `is_available` sütununu ekleyen `040` phase B pozisyonunda ileri/geri migration dosyası sağlamak; manifeste `040` kaydını eklemek ve manifest testindeki beklenen kimlik listesi, sayım ile son pozisyon tablolarını güncellemek.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Production kodunu bu governance görevi içinde doğrudan değiştirmek; kod değişiklikleri ilgili `V1-RMD` görevlerine aittir.
- HTTPS sertifika stratejisi, performans testi, yedekleme mekanizması ve modül domain incelemeleri; sonraki dalgalara aittir.

## Dependencies

- V1-GOV-039

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-074..078`) ve kapanış görevi (`V1-GOV-041`).
- Genişletilmiş migration manifesti.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-074
