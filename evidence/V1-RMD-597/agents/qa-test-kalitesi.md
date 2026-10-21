# qa-test-kalitesi — V1-RMD-597 ham rapor özeti

231/231 kalem tarandı. Taban: 333 .NET test + 23 vitest yeşil, 0 Skip. Mutasyon: 98 mutant; 44 öldü, 46 hayatta kaldı, 7 derleme hatası (sayılmadı). Kanıt düzeyi Çalıştırıldı (mutasyon sonrası test yeşil kaldı). Ham kayıt: tmp/qa/V1-RMD-597/qa-test-kalitesi/results.txt.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| T1 | Yüksek | ManagementSessionLookup.cs:17-39; mutantlar S1a,S2a,S3a,S5a,S1b,S2b,S3b,S5b | Yönetim oturumu SQL'inde revoked_at/expires_at/active/`manager:` önek koşullarının hiçbiri HTTP testiyle sınanmıyor. |
| T2 | Yüksek | A18,A19,A1,A3 | PIN kilitliyken sayaç artırma/sıfırlama koruması ve kilit sınırı sınanmıyor (testler sabit 2026-08-02 saati kullanıyor). |
| T3 | Yüksek | A5,A6,A5t,A7,A7s | 5 yanlış / 15 dk varsayılanları Identity testlerinde sabitlenmemiş (5→4, 15→5 dk geçer). |
| T4 | Yüksek | A8,A15a,A15b,A17 | Unlock pasif kullanıcıyı reddetmiyor ve sınanmıyor; SetPin sayaç sıfırlama ve PIN kilit süresi dolunca sayaç testsiz. |
| T5 | Yüksek | DualScreenApplication.Endpoints.cs:189,234; tests'te auth/unlock, auth/pin, PIN_LOCKED sıfır | /auth/unlock ve /auth/pin için C# HTTP testi yok; E2E 423'ü sahte route ile üretiyor. |
| T6 | Orta | D1,I1 | Oturum ömürleri (30 gün, 8 saat) sabit değerle sınanmıyor. |
| T7 | Orta | D9,D11 | RevokeAllAsync ve reconnect Expired eşlemesi testsiz. |
| T8 | Orta | G3,G4,G5 | MatchesReplay'de RequesterUserId/PermissionCode/SubjectType karşılaştırmaları testsiz. |
| T9 | Orta | G12b | Parti içi priorInThisBatch sayacı sınanmıyor. |
| T10 | Orta | O1,O2,O5 | Uzlaştırmada bütçe sahibi / eylem sahibi / sıralama kontrolleri ayrı ayrı sınanmıyor. |
| T11 | Orta | H4,H5,H10,G14a,R1 | Temsil 24 saat sınırı, bills.discount, allowSupervisor, IsActiveAt sınırı testsiz. |
| T12 | Orta | G17a,G17c | Davranışsal sıkılaştırma izin listesi ve pencere testsiz. |
| T13 | Orta | G19a-d,G19g | RoleManagementService uzunluk sınırları ve revoke yetki kontrolleri testsiz. |
| T14 | Orta | PermissionSplitDatabase.cs:32-56; 089/090/091/093/132 testsiz | Tüm göç zinciri ↔ katalog eşitliği testi yok. |
| T15 | Orta | PersonalCompBudgetEscalationResolver.cs | Kişisel ikram bütçesi için doğrudan birim testi yok. |
| T16 | Düşük | PostgresAuthorizationGrantRepositoryTests.cs:51 Task.Delay(300); G8 | Kilit testi gerçek zaman beklemesine dayanıyor; servis düzeyi kilit testsiz. |
| T17 | Düşük | authorization-decisions/client.ts; workspace.test.tsx:29-31 | client.ts'in kendi testi yok (yorum yanlış); self-approval mesajı arayüzde sınanmıyor. |

Temiz: kendi talebini onaylama (H1,H2), G1/G2, temsil miktar/süre/kendine devir, çevrimdışı bütçe Admits/süre, politika sınırları, oturum çekirdeği, parola/PIN eşik+süre çekirdeği; 0 Skip, mock yok (gerçek PG).
Doğrulanamayan: Host.Tests login ucu; G6,G8,H7 derleme/uygulama hatası; vitest mutasyonu (node_modules yok); yönetici PIN kilit açma uçtan uca senaryosu.
