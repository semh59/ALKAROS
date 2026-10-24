# qa-dokuman-bagimlilik - V1-RMD-601 (SHA c794e951)

Kapsam: 123/123 (20 csproj/lock tam, 16 migration (010/023/024/025/039 tam), 87 kaynak/test taraması, 8 doküman + 4 SKILL koda karşı). plan_audit validate: 0 hata/0 uyarı. Hiçbir bulgu çalıştırılmadı (kod okuması).

| No | Önem | Kanıt | Sorun / etki / öneri |
|---|---|---|---|
| D1 | Orta | `Table.cs:61-68`, `docs/domain/lifecycle-transition-contracts.md:96` | Doküman geçiş matrisi koddan sapar (Reserved→Occupied, Occupied→Cleaning, Cleaning→Occupied/OutOfService yok; "Occupied→Reserved" kodla örtüşmez); table-reservation-policy ile de çelişir. |
| D2 | Orta | `TableManagementContracts.cs:288`, `PostgresTableReservationRepository.cs:178`, test `PostgresTableReservationTests.cs:240-244` | Occupied için "Reserve" komutu sunulur, depo her zaman reddeder. İstemcide düğmenin görünüp görünmediği doğrulanamadı. |
| D3 | Orta | `docs/data/projection-ownership.md:17` | `TableStateChanged` olayı kodda yok; işaretçi sapma tespiti/yeniden kurma yalnız elle `current-pointer` ucundan; otomatik güvence yok. |
| D4 | Orta | `docs/data/nullable-unique-policy.md:28,31`, 010/025 | `zone_tables` tablosu yok; "tek aktif rezervasyon" için DB tekil kısıtı yok (yalnız kod). |
| D5 | Orta | `TableEventOrderConsumer.cs:14`, `TableIntegrationEvents.cs:41`, `OutboxFanoutSink.cs:10` | "At-least-once, idempotent" iddiası yalnız `WHERE table_id=@from`; yeniden teslim/sıra bozulması testi yok; TableMerged yeniden denemesi araya giren TableUnmerged'dan sonra siparişi tekrar ana masaya taşıyabilir (çıkarım). |
| D6 | Orta | `TableManagementStore.cs:86`, 098 | Teknik KASA-1 masası listeden filtrelenmez; istemcide görünüp görünmediği doğrulanamadı. |
| D7 | Orta | `api-contract-standard.md:38`; Tables'ta IdempotencyKey 0 | X-Idempotency-Key gereği Tables'ta yok (yalnız row_version); `alkaros-api-endpoint` SKILL'i Tables'ı bilinen sapma saymaz. |
| D8 | Orta | `table-reservation-policy.md:26,34`, `Application.cs:325`, `models.ts:76` | Politika "süre dolumu yok" der; kodda `expires_at` ve elle `/expire` var, zamanlayıcı yok. |
| D9 | Düşük | `table-reservation-policy.md:75`, `V1-TBL-004...md:34` | V12-QRO-003 "beklemede" yazıyor, görev Done. |
| D10 | Düşük | `single-branch-key-strategy.md:24`, 010 | Doküman "table_number sayısal/mağaza başına"; kod TEXT, bölge başına tekil. |
| D11 | Düşük | `api-contract-standard.md:107` | Örnek `tableId: 12`; gerçek UUID. |
| D12 | Düşük | `docs/design/modules/check-and-table.md:17,79` | Var olmayan `OrderManagementStore.cs:298-314`'e atıf; "Paid yapan uç yok" eski; tarihsel notu yok. |
| D13 | Düşük | `TableState.cs:5-6` | Yorum "Reserved yalnız QR akışından" der; kasiyer elle rezervasyonu da üretir. |
| D14 | Düşük | `module-dependency-rules.md:48,121-122`, `Transfer.cs:245,266,376`, `PaymentAwareTableTopologyPolicy.cs:60` | Tables "direct-call: none" ama billing/orders/payments okur ve audit_events'e doğrudan INSERT yapar (IAuditEventStore kullanmaz). |
| D15 | Düşük | `evidence/V1-TBL-001/closure-evidence.txt:13` | Var olmayan test adlarına atıf (tarihsel). |
| D16 | Düşük | `Directory.Packages.props:11` | NetArchTest.Rules 1.3.2 nuspec'inde lisans yok; politika izlemiyor; lisans doğrulanamadı. |

Tarihsel (bulgu değil): V1-TBL-008/009/010, V1-RMD-100, V1-CUI-006, V1-SET-003, V12-QRO-002 evidence yok (kural 2026-09-28 sonrası); V13-TBL-001 kapanışı bilinçli olarak "ödemesi sonuçlanmış kısmen ödenmiş hesap taşınabilir" diye kayıtlı.
Temiz: doküman .md atıfları, olay adı biçimi, order.json göç sırası, 10 lock dosyası sürümleri (Npgsql 9.0.2, xunit 2.9.2, FluentAssertions 6.12.0), NuGet/npm lisansları politika listesinde (PosTerminal 115 girdi, GPL/AGPL yok; MPL-2.0: axe-core, lightningcss).
Doğrulanamayan: D-1 (düğme gerçekten görünüyor mu), D-2 (KASA-1 istemcide), D-3 (V13-TBL-001 kanıt klasörü), D5 gerçek sıra bozulması, transitif lisanslar, NetArchTest lisansı.
