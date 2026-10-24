# qa-sinir-mimari - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 12/12 sınır kalemi (2 csproj/IModule, ModuleRegistry, 5 Host/Experience/Billing dosyası + Receipt, TableEventBillConsumer, 10 göçün çapraz şema FK'leri, 35 src public tip taraması, DI kayıtları, Host kenar listesi, outbox). Çalıştırıldı: consistency_audit.py exit 0 (clean); tests/Architecture/ModuleBoundaries 17/17 geçti, exit 0. Çıktılar tmp/qa/V1-RMD-603/qa-sinir-mimari/{arch,ca}.txt.

| No | Önem | Kanıt | Sorun | Etki |
|---|---|---|---|---|
| B1 | Orta | PaymentRefunds.cs:451-456; Bill.cs:180-182 | Tam iade edilen Paid fatura Host'ta ham SQL ile Cancelled yapılıyor; Bill.CanTransitionTo matrisi (Paid->Cancelled yok) atlanıyor. | Alan kuralı ikinci kez yazılmış, matris ayrışır |
| B2 | Orta | BillCancellation.cs:62-74; PaymentRefunds.cs:453-465; Host/Experience/Billing'de Outbox/EnqueueAsync yok | İptal/iade/bölme Host orkestratörlerinde ham SQL; durum değişikliği için olay yayınlanmıyor; IBillRepository kullanılmıyor. | Olay kaybı, tüm geçişleri kapsayan olay imkânsız |
| B3 | Orta | module-dependency-rules.md:50 (satır 5); grep BillStateChanged src/tests = 0 | Dokümanda onaylı BillStateChanged olayı yayınlanmıyor/tüketilmiyor; satır 4'teki gibi "planned" işareti yok. | Fiscal/Reporting/Reconciliation bağlı varsayılıyor (doküman sapması) |
| B4 | Düşük | BillPaymentClosureModule.cs:7-14 vs ModuleRegistry.cs:80 | Yorum "henüz DefaultCatalog'a eklenmedi" diyor, tip kayıtlı. | Bayat yorum |
| B5 | Düşük | BillingModule.cs:23,24,28 (Transient) vs BillingSplitApplication.cs:40,41,58 ve OrderManagementEndpoints.cs:146 (TryAddSingleton) | IBillRepository/ISplitDesignRepository/IBillAdjustmentRepository çift kayıt, farklı ömür. | Tam Host'ta zararsız; bağımsız kompozisyonda davranış ayrışır |
| B6 | Düşük | BillingSplitApplication.cs:128-202 | İndirim endpoint'inde izin, rol çözümü, yüzde->tutar ve grant dallanması (iş mantığı). | Yeniden kullanım/test HTTP'ye bağlı |
| B7 | Düşük | 019-billing.up.sql:27,28,55,56; 108-...up.sql:22,24,28; BillingModule.cs:19 | DB düzeyinde Tables, Catalog, Identity FK'si var; modül DependsOn yalnız Orders, dokümanda yok. | Göç sırası/geri alma bağımlılığı belgesiz |
| B8 | Düşük | BillingSplitStore.cs:352-370 | table_mgmt.tables.current_bill_id güncellemesi ayrı bağlantıda, NpgsqlException yutuluyor, günlük yok. | Sessiz işaretçi kaybı |
| B9 | Düşük | tests/Modules/Billing'de TableEventBillConsumer yok (kullanım Tables testlerinde) | Tek olay tüketicisi kendi projesinde sınanmıyor; ikinci teslim/eski olay doğrudan testsiz. | Regresyon geç yakalanır |

Çevre gözlemi: PaymentTender.log 1/53 kırmızı (ACheckThatWasAlreadySentToTheTillCompletesButItsTableIsNeverTouched), Billing.log yarım (sonuç/exit yok).

Temiz: modül kenarları (Billing DependsOn Orders; PaymentClosure test sözlüğüyle birebir); döngü yok; Host onaylı kenarlar dokümanla birebir; ham SQL yazımları yalnız billing.* + belgeli soft-cache istisnası; TableEventBillConsumer tek transaction/idempotent/dead-letter; sahipsiz public tip yok; closure servisleri kayıtlı.

Doğrulanamayanlar: HostOrchestrationEdgesStayWithinTheApprovedList kapsamı tek tek sınanmadı; Billing.log takılması altyapı mı gerçek mi; BillStateChanged için "planned" ibaresinin bilerek atlanıp atlanmadığı; remaining-gaps-register'da Billing/BillStateChanged kaydı yok.
