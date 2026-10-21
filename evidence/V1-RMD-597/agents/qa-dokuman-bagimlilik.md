# qa-dokuman-bagimlilik — V1-RMD-597 ham rapor özeti

231/231 dosya HEAD'de var; 78 göç up/down çifti tam; 57 görev dosyası tarandı; 28 NuGet + npm lisansları okundu. Kanıt düzeyi Kod okuması. counts.txt "214" ↔ gerçek 231 (217 de tutmuyor).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| 1 | Yüksek | PostgresUserStore.cs:230-244; IUserStore.cs:68-70 | Kabul 5: yönetici PIN kilidini açamaz (ForceUnlock yalnız parola alanları); V1-RMD-151 PIN sıfırlamayı kapsam dışı bırakmış. |
| 2 | Orta | DESIGN.md:145-146 vs AuthenticationService.cs:20-21 | Doküman sapması: şartname 3 hatada 30 sn + 5'te oturum iptali; kod 5'te 15 dk PIN kilidi. |
| 3 | Orta | authorization-model.md:43-63 vs ApplicationPermissions 26 kod | Belge 16 kod; 11 kod (orders.transfer-server*, bills.cancel, payments.refund.*, cash.session.override, reconciliation.manage, observability.manage, security.manage, integrations.manage, reports.close-day) ve rol matrisi yok; pos.cashier.mutate alias cümlesi eski. |
| 4 | Orta | AuthorizationDecisionEndpoints.cs:30 | Karar izni reports.view; belge "manager/supervisor" diyor. |
| 5 | Düşük | BehaviouralRateAssessor.cs:19-28; authorization-model.md:36-39 | Belge void %, comp ₺/shift diyor; kod yalnız onaylı talep sayısı. |
| 6 | Düşük | authorization-model.md:193 | DESIGN.md §1'de olmayan ilkeye atıf. |
| 7 | Düşük | PermissionSplitDatabase.cs:6-23 | Sınıf özeti eski (112/163; liste 181/184). |
| 8 | Düşük | ~/.claude/skills/alkaros-api-endpoint/SKILL.md | "13 MapError" artık yok (ApiErrorCatalog). |
| 9 | Düşük | module-dependency-rules.md:168-177; ModuleBoundaryTests.cs | Authorization, Roles, OfflineReconciliation, ManagementSessionLookup ApprovedHostOrchestrationEdges dışında. |
| 10 | Düşük | V1-RMD-597 görev metni | ManagementSessionLookup yolu yanlış (src/Host/Experience/...). |
| 11 | Düşük | V1-RMD-097 | Done görev Owned yolu taşınmış (tests/BuildingBlocks/Messaging). |
| 12 | Düşük | evidence/V1-RMD-405/tests.log | Done görevde 1 kırmızı (restore drill pg_dump sürümü). |
| 13 | Düşük | V1-IAM-029 test adı | Eski test adı atfı. |
| 14 | Orta | pnpm-lock.yaml; third-party-license-policy.md | npm lisansları politika dışı: axe-core/lightningcss MPL-2.0, fetch-cookie Unlicense, MIT-0, ISC; politika npm'i kapsamıyor (PO kararı). |
| 15 | Düşük | policy; Npgsql PostgreSQL lisansı; 4 geçişli pakette license alanı yok | Bilinen (596): Npgsql karar açık. |
| 16 | Düşük | ALKAROS.Identity.Authentication.Tests.csproj:14 | Kullanılmayan doğrudan Npgsql PackageReference. |
| 17 | Düşük | Host testleri 1'er dosya | Ölçü notu (test-kalitesi). |

Temiz: dosya varlığı, göç çiftleri, 6 oturum rotası, 8 saat/30 gün ömür, eşikler, çevrimdışı TTL 4 saat, kendi talebini onaylama, 6 rol ve matris, borç kalıpları yok, NuGet lisansları, kırık atıf yok.
Doğrulanamayan: derleme/test; Logging.Abstractions lock 8.0.3/8.0.2; 214↔231 nedeni; MPL paketlerinin ürün çıktısına girip girmediği.
