# Kullanıcı gözü: eksik avcısı (kod okuma; her "yok" için istemci dizinlerinde grep, sonuç 0)

Ulaşılamayan ekranlar (ekran var, bağlantı yok)
1. [Yüksek] PosTerminal App.tsx:63-98 - /settings/relay, token-terminal, qnb-credential, screensaver, business-identity, security yalnız elle URL ile açılıyor; hiçbir link/menü yok. Fiş, e-fatura ve güvenlik ayarlarına bağlantı yok.
2. [Orta] PosTerminal routes/Cashier.tsx:552 - Yönetim bağlantısı yalnız reports.view yetkisine bağlı; ayar ekranları Yönetim sekmelerinde de yok.

Ekranı olmayan backend yetenekleri
3. [Yüksek] SecurityAdministration/OutboxAdministration.cs:33,63 - /outbox/dead-letters ve requeue; ölü mektup görünmüyor, yeniden kuyruğa alınamıyor.
4. [Orta] SecretRotationAdministration.cs:38-112 - gizli anahtar listesi ve initialize/rotate/revoke/rollback yalnız API.
5. [Orta] KitchenOperationsEndpoints.cs:483,501 - /audit/aggregate ve /audit/correlation ekranı yok.
6. [Orta] OnlineOrderReconciliationEndpoints.cs:43,54,76 - scan/retry/resolve; scan tetikleyicisi UI'da yok.
7. [Düşük] TableManagementApplication.cs:227,348 current-pointer ve rezervasyon expire; MenuManagement :133 reorder; ProductionManagement :115,184 batches/by-number ve recipe-version; Observability :57,170 alerts/raise ve POST health-checks.

İstemciler arası fark
8. [Orta] WaiterPwa'da masa birleştirme, ayırma ve rezervasyon yok (PT'de var).
9. [Düşük] PosTerminal'de kurs ateşleme yok (WA'da var); WaiterPwa'da ürün askıya alma yok.

Not: Settings, RecipeCostSnapshot, EOD, Reconciliation, Observability için "UI yok" kayıtları artık eski (PT'de çağıranı var); pending-frontend-for-dead-module-wiring notu güncellenmeli.
Doğrulanamadı: yetki-menü görünürlüğü tek tek, Done görev-ekran karşılaştırması.
