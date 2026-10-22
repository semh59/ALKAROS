# V1-RMD-599 Host KVKK ve saklama - qa-mevzuat-uyum
SHA: d1dc86b6. M=24 dosya okundu (kvkk-retention-runbook, data-housekeeping, V0-CMP-003 envanteri, compose.ops.yaml, Program.cs housekeeping, migration 038/096/100/101/119/009, WebPush 4 dosya, HelpRequestStore, ServingHandoffNoteStore, SignalRPendingOrderAnnouncer, AccountRecoveryService, PostgresUserStore, CustomerAccountsStore, DualScreenApplication.CustomerAccounts, DualScreenStore.QrBill). Host .cs sayisi 187 (find src/Host -name *.cs, obj/bin haric). Salt okunur; build/test kosulmadi. Kanit duzeyi hepsi Kod okumasi. Mevzuat yorumu yapilmadi; yorum gerektirenler Semih sorusu.

## Bulgular
| No | Onem | Kanit | Sorun | Etki | Onerilen duzeltme |
|---|---|---|---|---|---|
| 1 | Yuksek | kvkk-retention-runbook.md:15-20; kvkk-data-inventory.md:12-20; 096-push-subscriptions.up.sql:3-60; 100-serving-handoff-notes.up.sql:16-26; 101-help-requests.up.sql:11-30 | notifications.push_subscriptions, serving_handoff_notes, help_requests KVKK envanterinde ve runbook icinde yok; saklama suresi, imha eylemi, sahip tanimsiz (grep push_subscriptions/handoff/help_requests docs/compliance ve envanter: 0). Kodda var, kaynakta yok. | Kisisel veri (kullanici kimligi, cihaz, serbest metin) siresiz kalir | Envantere satir ekle, sure kararla (S1), retention_rules sinifi ekle |
| 2 | Orta | 100-serving-handoff-notes.up.sql:11-12; ServingHandoffNoteStore.cs:59-61,97-99 | Migration yorumu not okunduktan sonra gider der; kod yalniz consumed_at isaretler, satir silinmez; okunmayan not da bitmez (grep DELETE serving_handoff_notes: 0). Serbest metin 200 kr (ServingHandoffNoteStore.cs:26). | Kisisel veri icerebilen metin siresiz; siparis notuna uygulanan 5 yil kurali bile yok | Tuketilen/eski notu silen is; S1 |
| 3 | Orta | 101-help-requests.up.sql:11-30; HelpRequestStore.cs:105-107 | help_requests (kullanici id, masa, tur, zaman) icin temizlik yok; kodda yalniz son satir bekleme icin okunur (grep help_requests src: Store disinda 0 okuyan). Migration yorumu denetim izi der, ama okuyan uc yok. | Personel davranis kaydi siresiz birikir | TTL veya audit olayina tasima; S1 |
| 4 | Orta | 096-push-subscriptions.up.sql:22,28; PushSubscriptionStore.cs:42-47,116-123 | Push endpoint URL'si cihaz+kullanici bagli tanimlayici; envanterde siniflanmamis; TTL yok (last_success_at yazilir, silmeye baglanmaz; yalniz 404/410 silinir). | Terk edilmis abonelik siresiz kalir | Envantere ekle (S3); N gun basarisiz/eski aboneligi sil |
| 5 | Yuksek | AccountRecoveryService.cs:84-95; WebPushSender.cs:73,99-110 | Kullanici pasiflestirilince yalniz oturumlar iptal edilir; push_subscriptions silinmez. Abonesi olmayan kullanici icin BroadcastAsync tum aboneliklere gider; GetAllAsync kullanici aktifligine bakmaz. Ayrilan personelin cihazi Misafir siparisi/siparis hazir bildirimi almaya devam eder (SignalRPendingOrderAnnouncer.cs:55-58; KitchenOperationsStore.cs:318). | Ayrilan kisiye operasyonel veri sizar; erisim kesme eksigi (hukuki yorum: karar gerekir, S6) | DeactivateAsync icinde abonelikleri sil; yayini aktif kullanicilarla sinirla |
| 6 | Orta | Program.cs:286,297-300; data-housekeeping.md:5-8,31-34; compose.ops.yaml:58-61 | Housekeeping yalniz idempotency_keys ve device_sessions siler (belgeli). push_subscriptions, handoff, help_requests, customer_display.display_sessions ve pairing_requests (expires_at/revoked_at kolonlu, 038-dual-screen-pos.up.sql) icin temizlik yok. Bilinen (veritabani ajani da belirtti). | Sureli kayitlar siresiz; display_sessions token_hash tutar | Housekeeping kapsamini genislet |
| 7 | Orta | kvkk-retention-runbook.md:17; 096:28; 100:18-19; 101:19 | Anonimlestirmede users satiri silinmez, bu yuzden ON DELETE CASCADE hic tetiklenmez; aboneler, not metni, yardim cagrilari anonimlestirilen kisiyle iliskili kalir. Yeterlilik yorumu S2. | Anonimlestirme eksik | Retention planina bu tablolari ekle |
| 8 | Orta | 096-push-subscriptions.up.sql:53; PushSubscriptionStore.cs:182-190 | VAPID private_key duz TEXT. Aramada (Encrypt|SensitiveEnvelope|DataProtection, src/Host/Experience/WebPush) yalniz RFC 8291 mesaj sifrelemesi var, anahtar sifreleme 0; repoda src/BuildingBlocks/Security/SensitiveData/SensitiveEnvelope.cs mevcut ama burada kullanilmiyor. Migration yorumu anahtarin settings'ten ayrik olmasini gerekce yapar. | Yedek/dump ele gecirilirse push kimligi calinir | SensitiveEnvelope veya secret deposu; risk kabulu S4 |
| 9 | Yuksek | CustomerAccountsStore.cs:95-117; DualScreenApplication.CustomerAccounts.cs:74-86 | Musteri vergi kimligi (VKN/TCKN) guncellemesi ve sunucuda Manager rolu ile tam profil cozumu hicbir denetim/erisim kaydi yazmaz (grep Audit CustomerAccountsStore.cs ve PostgresCustomerProfileStore.cs: 0). Yetki yalniz PaymentsTake (DualScreenApplication.CustomerAccounts.cs:77). PO karari: yetki izi ve erisim kaydi 10 yil saklanir; burada kayit uretilmiyor. G-09 etiketi repoda bu konuyla eslesmiyor (evidence/V1-RMD-398 G-09 stok konusu; V1-RMD-597 G-09 cevrimdisi sure) - numara dogrulanamadi, bulgu kodla dogrulandi. | Kim hangi musterinin kimligini degistirdi/okudu izlenemez | Degisiklik ve cozum icin audit olayi (alan, maskeli eski/yeni, aktor); 10 yil saklama; S5 |
| 10 | Dusuk | kvkk-retention-runbook.md:24 | Dokuman sapmasi: pin_hash temizligi V1-RMD-673 ile izleniyor; kodda dogrulanmadi (Identity kapsami). | - | Identity ajanina birakildi |
| 11 | Dusuk | 038-dual-screen-pos.up.sql; 119-customer-display-screensaver.up.sql | customer_display.* dogrudan kisisel veri tasimiyor. Envanterde Device data: cihaz omru, kaldirmada sil denir; cihaz kaldirma silme yolu yok (grep decommission src/Host/DualScreen: 0). | Kaldirilan cihaz satirlari kalir | Silme yolu veya eslesme notu |

## Temiz bulunan alanlar
- Host log: kisisel alan loglayan Log* cagrisi yok (grep -E Log(Information|Warning|Error) ile username/displayName/phone, src/Host: 0). WebPushSender yalniz durum kodu loglar.
- Push mesaj govdesi kisisel veri tasimaz (masa no, kalem sayisi, urun adi): SignalRPendingOrderAnnouncer.cs:55-58.
- Push silme ucu sahip kontrollu (PushSubscriptionStore.cs:140-143); endpoint https zorunlu (WebPushExperience.cs).
- Musteri ekrani ve QR canli hesap DTO'lari ad/telefon tasimaz (DualScreenContracts.cs, DualScreenStore.QrBill.cs).
- device_sessions temizligi grace ile calisiyor, session_operations CASCADE; idempotency_keys expires_at ile siliniyor (Program.cs:286-300).
- Bahsis/servis ucreti bu dilimde yok.

## Semih sorulari (urun/hukuk, karar gerekir)
- S1: Devir notu ve yardim cagrisi kayitlari icin saklama suresi (tuketimden sonra N gun mu)? Yardim cagrisi denetim izi (10 yil) mi sayilacak, operasyonel mi? Repoda kaynak karar yok.
- S2: Anonimlestirilen personelin push aboneligi, devir notlari, yardim cagrilari silinsin mi, anonim mi kalsin?
- S3: Push endpoint URL'si kisisel veri sayilsin mi?
- S4: VAPID ozel anahtari DB'de duz kalabilir mi (risk kabulu), sifrelensin mi?
- S5: Musteri vergi kimligi icin 10 yillik erisim kaydi kapsami: yalniz degisiklik mi, her cozumleme mi?
- S6: Pasif personelin cihazina yayin bildirimi gitmesi kabul edilebilir mi?

## Dogrulanamayanlar
- Kanun/madde esikleri repoda yok; yorum yapilmadi.
- Host disi modullerde (Identity, Orders) log icerigi kapsam disi.
- pin_hash temizligi (V1-RMD-673) dogrulanmadi.
- Park edilen V1-RMD-474, 519, 572, 588 bu dilimle iliskilendirilmedi (bilinen).
