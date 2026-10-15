# V1-RMD-550 karar kaydı — izleme sistemi

Tarih: 2026-10-15. Dayanak: PO kararları (2026-10-15) ve bağımsız plan denetimi bulguları. Bu kayıt kod değiştirmez; V1-RMD-551…557 ve 533…549 görevlerinin dayanağıdır.

## Kararlar

1. **Mevcut uyarı çatısı tek kaynaktır.** Hata olayları `src/Modules/Observability/AlertFoundation` (V1-ALT-001, Done) üzerinden uyarı açar; olayın parmak izi uyarının tekilleştirme anahtarıdır. Onayla, yükselt, bastır, çöz yaşam döngüsü yeniden yazılmaz.
2. **Ekran mevcut Sistem bölümünü genişletir.** Yönetimdeki Sistem bölümü (aktif uyarılar ve sağlıksız kontroller) V1-RMD-539'un çıkış noktasıdır; paralel ikinci bir ekran kurulmaz.
3. **Telegram içeriği (PO 2026-10-15): yalnız sabit künye.** Kategori, sayı, saat, terminal etiketi. Hata metni, müşteri, tutar, ürün adı, kullanıcı adı gitmez. Gerekçe: Telegram yurt dışı bir hizmettir; içerikte kişisel veri olması KVKK açısından risktir (hukuki görüş doğrulanmadı).
4. **Dalga (PO 2026-10-15): hepsi ilk dalgada.** Z-skor şiddeti (536), zaman çizelgesi (548), derin veritabanı izleme (546) ve gerileme tespiti (544) ilk dalgadadır. Bağımsız denetim bunların ikinci dalgaya alınmasını önermişti; PO ilk dalgada kalmasını seçti. Risk: ilk günlerde taban çizgisi verisi az olacağından 536 ve 544 uyarıları erken dönemde güvenilir olmayabilir; bu yüzden en az örnek ve en az mutlak sayı koşulları görev kabul koşullarındadır.
5. **Gizlilik çekirdekten önce gelir.** V1-RMD-549, veri yazan her görevin bağımlılığıdır.

## Mevcut işlerle örtüşme tablosu

| Mevcut iş | Durum | Bu planla ilişki | Karar |
|---|---|---|---|
| V1-ALT-001 uyarı çatısı | Done | Olay → uyarı köprüsü ve tekilleştirme anahtarı | Yeniden kullanılır, değişmez |
| V1-OBS-001 sağlık kaydı | Done | Sağlık kontrolü kayıtları; görüntüyü yazan çağıran yok | V1-RMD-555 çağıranı bağlar |
| V15-OBS-001 yapılandırılmış günlük | Done | Olay günlüğü ve `CorrelationContext` | V1-RMD-542 mevcut bağlamı genişletir |
| V15-OBS-002 sağlık ve uyarılar | Planned | Disk, yazıcı, yedek, entegrasyon dedektörleri 535/547/555 ile aynı alan | 535/547/555 bu işin kodunu üretir; V15-OBS-002 kapsamı karşılandığında bu kayıt kanıt olarak gösterilir. Kaydın kendisi bu görevde değiştirilmedi (sahiplik). |
| V15-OBS-003 saklama | Planned | Mevcut sağlık, uyarı olayı, inbox/outbox, denetim tabloları | 549 yalnız yeni izleme tablolarını kapsar; mevcut tablolar V15-OBS-003'te kalır (kısmi örtüşme) |
| V15-NOT-001 bildirim | Planned | Kanal soyutlaması, retry, tekilleştirme, yükseltme; V15-OBS-002 ve V0-ARC-006'ya bağlı | 538 şimdilik kendi sınırlı kuyruğunu kurar; V15-NOT-001 gelince Telegram o kanal sözleşmesine taşınır (yeniden yazım riski kabul edildi) |

## Açık maddeler

- KVKK: çalışan davranışı izlemesi (düğme etkileşimi) için aydınlatma metni ve hukuki görüş gerekir; **doğrulanamadı**, sahibi PO/hukuk.
- Telegram bot hesabı ve sohbet kimliği PO tarafından açılacak; V1-RMD-557 bu hesap gelene kadar bekler.
- V15-OBS-002 ve V15-NOT-001'in dış cihaz/sağlayıcı zincirine (V0-HUG-001, V0-QNB-001) bağlı olması bu planı engellemez; ilgili dedektörler mevcut kayıtlardan okur.
