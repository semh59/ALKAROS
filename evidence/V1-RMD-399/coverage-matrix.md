# V1-RMD-399 kapsam matrisi (kod okunmadan önce yazıldı)

Kaynak: görev Goal adımları + `docs/domain/authorization-model.md` §3, §4, §5, §8. Her satır en az bir probe ile
sınanır; probe kimliği ve sonuç `REPORT.md` içinde bu tabloya bağlanır.

| # | Goal adımı | Invariant (doğru davranış) |
| --- | --- | --- |
| A1 | Giriş | Yanlış parola oturum vermez; art arda hatalar hesabı kilitler (V1-IAM-004). |
| A2 | Oturum sonu | Çıkış/iptal sonrası aynı çerez bir sonraki istekte 401 alır (V1-IAM-007). |
| A3 | Oturum ömrü | Süresi dolmuş oturum reddedilir (V1-IAM-031, 8 saat). |
| A4 | Oturum türü | Müşteri ekranı (display) oturumu kasiyer/yönetici mutasyonu yapamaz. |
| A5 | Yönetimsel iptal | `revoke-sessions` / devre dışı kullanıcı bir sonraki istekte etkilidir. |
| B1 | Rol kataloğu | Tohum rol→izin matrisi §3, §3.1, §3.2 ile birebir aynıdır. |
| B2 | Canlı okuma | Rolden izin kaldırmak bir sonraki istekte etkilidir (§8, önbellek yok). |
| B3 | Rol yönetimi | Rol/kullanıcı yönetimi yalnız yetkili rolde; kimse kendine üst rol veremez. |
| C1 | Dört göz | İsteyen kendi grant isteğini onaylayamaz / reddedemez (V1-RMD-316 geri dönüş). |
| C2 | Tek kullanım | Bir grant tam bir komut örneğini yetkilendirir; ikinci kullanım reddedilir (§8). |
| C3 | Kendi hesabı | Garsonun başka garsonun hesabındaki void/comp isteği otomatik reddedilir (§3 karar 1). |
| C4 | Delegasyon | Süresi dolan veya iptal edilen delegasyon uygulanmaz; kimse kendine delegasyon veremez; tutmadığı izni devredemez. |
| C5 | Davranışsal kısıt | Kişi kendi kısıtını temizleyemez (V1-RMD-316). |
| C6 | Karar yüzeyi | Karar uçları yalnız manager/supervisor + `reports.view`. |
| C7 | Çevrimdışı bütçe | Başka oturumun `budget_id`'si kullanılamaz; aynı eylem iki kez harcanamaz (§5, §8). |
| D1 | Uç nokta — kimliksiz | Kamusal olmayan her rota oturumsuz istekte 401 döner (dinamik tarama). |
| D2 | Uç nokta — izinsiz | İzinsiz oturum her mutasyon rotasında 403 alır; yalnız belgelenmiş oturum-yeterli rotalar hariç. |
| D3 | Uç nokta — rol sınırı | Garson §3'te ❌ olan eylemleri (rezervasyon, bölme, kasa, katalog) yapamaz. |
| D4 | Uç nokta — oturum türü | Yönetim rotaları terminal kasiyer oturumunu yönetici oturumu yerine kabul etmez ya da bu bilinçli karardır. |
| E1 | Sahiplik — terminal | Terminal A oturumu yol parametresinde terminal B ile işlem yapamaz. |
| E2 | Sahiplik — hesap | Terminal A oturumu başka terminalin hesabına dokunamaz ya da bu bilinçli karardır. |
| E3 | Sahiplik — garson siparişi | Garson başkasının siparişini değiştiremez ya da bu bilinçli karardır. |
| F1 | Güvenlik yönetimi | Kilit açma / oturum iptali yalnız yönetici; hedef kullanıcı kendisi olamaz ya da bilinçli. |
| F2 | Kullanıcı yönetimi | Parola/PIN sıfırlama ve kullanıcı oluşturma yetkisiz rolde 403. |
| F3 | Kamusal yüzey | Anonim rotalar (QR, NFC, webhook, müşteri ekranı) personel verisi veya mutasyon sızdırmaz. |
