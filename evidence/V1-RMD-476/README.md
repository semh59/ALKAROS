# V1-RMD-476 - Yeni online sipariş sesi her ekranda ve online sekme yetkileri

Kasa satış ekranında (ve Siparişler dışındaki her ekranda) yeni QR ya da online sipariş düştüğünde ses çalar; iki online sekmenin yetkisi sunucunun istediğiyle hizalandı.

## Değişiklikler

- `features/online-operations/useNewOrderChime.ts`: `orders.create` yetkili oturumda sırayı (`source=all`) 20 saniyede bir okur, ilk okumayı temel alır, sonra görülmemiş sipariş için `playNewItemChime`. Hatalı okuma sessiz geçer, ekrandan çıkınca durur.
- `routes/Cashier.tsx`: kanca burada çağrılır. Ana satış ekranı (`/`) `ExperiencePage` kullanmadığı için ilk denemede (workspace içinde) satış ekranı kapsanmıyordu; gerçek Host denemesinde fark edilip taşındı.
  Siparişler sekmesi açıkken kanca kapalıdır (sekme kendi sesini çalar, çift ses olmaz).
- `features/online-hub/onlineHubApi.ts`: Siparişler sekmesi `orders.create` ister (yalnız `tables.status` olan oturum artık `/online` adresini elle açınca 403'lü boş sekme görmez); Sorunlar sekmesi listeyi okuyan `reports.view` ister.

## Kanıt

- `vitest.log`: 60 dosya, tüm testler yeşil (yeni: kanca 3, Cashier ses 2, workspace kapısı 1; hub ve workspace testleri yeni yetkiye göre güncellendi). `typecheck.log`, `lint.log`, `build.log` exit 0.
- `mutation-client.log`: yetki koşulu, ilk okuma tabanı ve iki sekme yetkisi bozulunca 6 test kırmızı; dosyalar geri alındı, `cmp` özdeş.
- `trial.live.log`: gerçek Host ve Postgres, tarayıcıda satış ekranında: sıra isteği `source=all` ile başladı, ses 0; Yemeksepeti webhook'u ile sipariş düşünce bir sonraki okumada `AudioContext` bir kez açıldı (ses çaldı), ekran satışta kaldı.

## Açık kalan

- Tarayıcı otomatik oynatma kuralı: ses, sayfada en az bir tıklama olduktan sonra çalar (kasiyer girişte tıkladığı için pratikte çalışır).
- Siparişler sekmesi dışında görsel uyarı yok, yalnız ses.
