# qa-sinir-mimari - V1-RMD-601 (SHA c794e951)

Kapsam: ~20/123 dosya satır satır (Tables kaynakları, iki tüketici, outbox, ModuleBoundaryTests, dependency-rules); kalan dosyalar grep ile tarandı. İlk koşu 1 saat sonra çıktı vermediği için durduruldu, ikinci (sınırlı) koşu raporu bu. Test/build yok; tüm bulgular kod okuması.

| No | Önem | Kanıt | Sorun / etki / öneri |
|---|---|---|---|
| S1 | Yüksek | `OutboxStore.cs:355`, `PostgresOrderRepository*.cs:233-235`, `TableEventOrderConsumer.cs:61-70`, `RetryPolicy.cs:13` | Merge/Unmerge olayları sırasız işlenebilir: Merge backoff'a girerse Unmerge önce teslim edilir (no-op), sonra Merge yeniden teslim edilip sipariş primary'de kalır. Aynı merge grubu için sıralı teslim veya durum tabanlı tüketici. (= findings-raw B005) |
| S2 | Yüksek | `OutboxStore.cs` (3 deneme sonrası dead), tüketicilerde telafi yok, `PostgresTablePointerProjector.cs:323,342` | Reparent olayı ölürse Tables commit'li, Order/Bill eski masada kalır; fark eden/geri oynatan mekanizma ve test yok. Uyarı + yeniden oynatma/uzlaştırma. |
| S3 | Orta | `Merge.cs:176-231`, `Transfer.cs:245-266` | Eşzamansız taşıma penceresi: payload yalnız masa kimliği; aradaki yeni sipariş de taşınır. Payload'a order/bill id listesi. (= B005) |
| S4 | Orta | `Merge.cs:342`, `Unmerge.cs:270`, `Transfer.cs:376`, `PostgresTableReservationRepository.cs:251,425,580,737`, `PostgresTablePointerProjector.cs:171` | 8 ham `audit.audit_events` INSERT; `IAuditEventStore` ve sanitizer atlanır; `module-dependency-rules.md:114-116`; consistency-audit kural 5 audit'i muaf tuttuğundan yakalamaz. Şu an kişisel veri sızıntısı görülmedi. |
| S5 | Orta | `TablesModule.cs:25-27`, `TableManagementApplication.cs:36-41` | Modül DI'ı bölünmüş: Merge/Transfer/Reservation/projector Host'ta kayıtlı; kayıt boşluğu/doküman sapması, işlevsel hata değil. |
| S6 | Düşük | `PaymentAwareTableTopologyPolicy.cs:60,89,134`, `module-dependency-rules.md:196-203` | `payments.payments` okuması + FOR UPDATE; dokümanda Payments için açık satır yok. |
| S7 | Düşük | `ModuleBoundaryTests.cs:82,25` | Mimari test yalnız C# referans kenarlarını görür; ham SQL okuma/yazma testsiz. |

Düzeltme: "Transfer/Merge billing/orders'ı günceller" iddiası yanlış; `src/Modules/Tables` içinde orders/billing/payments'a UPDATE/INSERT/DELETE 0, yalnız SELECT; taşımayı sahibi modülün tüketicileri yapar (yazma sahipliği ihlali yok).
Temiz: csproj başvuruları (Orders/Billing/Payments/Audit yok), `DependsOn` boş ve kodla uyumlu, sürümlü olay adları + aynı transaction'da outbox yazımı, tüketici kayıtları, Host kenarı onaylı listede.
Doğrulanamayan: S1 canlı yeniden üretim; merge sırasında primary'de açılan siparişin Unmerge'te akıbeti; `PostgresBillRepository.ReparentBillToTableAsync` SQL'i; kural 5'in Tables için tam kapsamı; ölü outbox uyarısı.
