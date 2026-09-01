# V1-RMD-077 - Cashier terminal session provisioning

- Task ID: V1-RMD-077
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Cashier istemcisindeki sabit terminal kimliğini kaldırmak. Host, kasiyer oturum çerezinden bağlı terminal kimliğini türeten bir uç nokta sunar; Cashier istemcisi sayfa açılışında bu kimliği okuyup tüm terminal kapsamlı isteklerinde kullanır, alınamazsa güvenli biçimde kapanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-077-cashier-terminal-session-provisioning.md`
- `src/Host/DualScreen/DualScreenApplication.cs`
- PO:2026-09-01 kararıyla src/Host/DualScreen/DualScreenStore.cs yüzeyi V1-RMD-090'a devredildi; bu historical task closed kalır ve oturtma sürüm toleransı 17. dalgada uygulanır.
- PO:2026-09-01 kararıyla src/Clients/Cashier/wwwroot/** yüzeyi V1-RMD-083’e devredildi; bu historical task closed kalır.
- PO:2026-09-01 kararıyla tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs yüzeyi V1-RMD-090'a devredildi; bu historical task closed kalır ve oturtma testleri 17. dalgada güncellenir.
- `evidence/V1-RMD-077/**`

## In scope

- `DualScreenStore` içinde kasiyer çerez token'ından bağlı terminal kimliğini `device_id` üzerinden çözen bir okuma metodu.
- `DualScreenApplication` içinde bu metodu kullanan, terminal kimliğini sorgu parametresi olarak gerektirmeyen bir oturum çözümleme uç noktası.
- Cashier istemcisinin açılışta bu uç noktadan `terminalId` ve `displayName` alması; sabit GUID'in kaldırılması.
- Terminal kimliği çözülemezse istemcinin oturum gerekli durumunu göstermesi ve sipariş göndermeyi kapatması.
- Satış istemcisine dönen katalog okuma yolunun `V1-RMD-076` kullanılabilirlik alanına göre kullanılamayan ürünleri dışlaması.
- Store metodu davranışının xUnit testi.

## Out of scope

- Kasiyer giriş veya cihaz eşleştirme akışını yeniden tasarlamak.
- Katalog yükleme veya sipariş gönderme mantığını değiştirmek.

## Dependencies

- V1-RMD-075

## Deliverables

- Çerezden türetilen terminal kimliği uç noktası ve sabit kimlik içermeyen Cashier istemcisi.

## Acceptance evidence

- `dotnet test` host dual screen testleri sıfır hata verir.
- Semih; geçerli kasiyer oturumuyla Cashier ekranının doğru terminal kimliğiyle çalıştığını, oturumsuz açılışta güvenli kapandığını doğrular.

## Handoff

- V1-GOV-041
