# qa-frontend — V1-RMD-597 ham rapor özeti

139/231 (17 istemci dosyası tam, 13 sunucu sözleşme dosyası, 24 tohum göçü betikle). Kanıt düzeyi Kod okuması; tsc/oxlint/axe koşulmadı; vitest 23/23 ana oturum kaydı.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| F1 | Yüksek | routes/workspace.tsx:542; AuthorizationDecisionsWorkspace.tsx:114 | Onay/ret/devir geri al/sıkılaştırma kaldır: catch yok, meşgul durumu yok; hata sessiz (SELF_APPROVAL_NOT_ALLOWED istemcide yok), çift tıkla çift POST. |
| F2 | Yüksek | offline-queue.js:371,373-374 | Çevrimdışı yetki kayıtları 5xx/429/503'te kalıcı silinir. |
| F3 | Yüksek | SecurityAdministrationEndpoints.cs:91; PostgresUserStore.cs:230-240; RecoveryPanel.tsx:59 | IsLocked yalnız parola kilidi; PIN kilidinde "Kilidi aç" pasif, force-unlock PIN'e dokunmaz. |
| F4 | Orta | offline-queue.js:228-233 | 401/403/408/409/423 kuyruk turunu kalıcı hatalıya taşır; 371'le çelişir. |
| F5 | Orta | waiter-app.js:140-148 | Çıkışta offlineQueue/failedOrders/offlineActions temizlenmez, sahibe bağlı değil; çıkış sonucu kontrol edilmez. |
| F6 | Orta | kiosk-lock.js:221-224 | Ağ yok/429/5xx/oturum bitti "PIN hatalı" gösterir, PIN siler. |
| F7 | Orta | kiosk-lock.js:128; waiter-app.js:145 | PIN kurulu bilgisi yalnız yerel bayrak; çıkışta silinir; yeni girişte kilit gelmez (pinSet yanıtta var, kullanılmıyor). |
| F8 | Orta | waiter-app.js:870-871,854 | Yenilemede kilit start() sonrası uygulanır; start hata verirse hiç; setInterval birikir. |
| F9 | Orta | StaffSection.tsx:77-78,43 | Bulunan kişi kartı arama kutusu değişince eski kalır; setActive sonrası yeni metinle yeniden arama; createUser aramayı siler. |
| F10 | Orta | StaffSection.tsx:36-43; panel.tsx:11-19 | Hesap açma/pasifleştirme revision artırmaz; rol atamak için ayrı panel; formda rol yok. |
| F11 | Orta | models.ts:53-54,58-61,66-67 | 12 sunucu sebep kodundan 10'u ham İngilizce basılır. |
| F12 | Orta | client.ts:3; workspace.tsx:537-538; http.ts | Hata sınıfı uyuşmuyor (AuthorizationDecisionApiError vs ApiError); sunucu Türkçe mesajı gösterilmez; 401'de oturum düşmez/yanlış mesaj (V1-RMD-291 kısmen bilinen). |
| F13 | Orta | AuthorizationDecisionsWorkspace.tsx:94,135,32 | Ham ISO/UTC tarih; talep eden UUID parçası; rol/konu gösterilmez; elle yenile; her yenilemede ekran boşalır. |
| F14 | Orta | RolesPanels.tsx; RoleManagementService.cs:84,104 | Kendi identity.roles.manage yetkisini geri alma/son yönetici koruması yok. |
| F15 | Orta | panel.tsx:11-19; workspace.tsx:539-544 | İstek sırası/iptal yok; eski yanıt yeniyi ezebilir. |
| F16 | Orta | index.html loginOverlay/lockOverlay; vanilla-clients-a11y.test.ts | Giriş/kilit katmanı ve iki özellik için axe testi yok; odak geri verilmiyor. |
| F17 | Düşük | management-staff-roles.css:9 | İzin satırı <48px; 40 izin düz liste. |
| F18 | Düşük | RolesPanels.test.tsx:11-19; workspace.test.tsx | Test 37 elle kod (canlı 40); onay yanıtı 204 taklit. |
| F19 | Düşük | kiosk-lock.js; waiter-app.js:584 | PIN üst sınırı yok, tuş takımı 12 hane. |
| F20 | Düşük | models.ts; Workspace.tsx | Metinler strings.ts dışında; toFixed(1) nokta; reddet/geri al onaysız. |

Doküman sapması: alkaros-frontend-screen SKILL "error.message gösterilmez" vs kod sunucu Türkçe message'ını basar; mutasyonlarda idempotency anahtarı yok; metinler strings.ts dışında.
Temiz: izin etiketleri ↔ 40 tohum kodu, rota-izin haritası, uç sözleşmeleri, rol/kullanıcı kapıları, lang=tr/para, 48px hedefi, PWA PIN çift dokunma koruması, Cashier çıkışı.
Doğrulanamayan: tsc/oxlint/axe; supervisor oturumu + security.manage 401 durumu; localStorage kota; delegasyon ekranı yok (V1-RMD-296 bilinen), indirim istemcisi (V1-RMD-292 bilinen).
