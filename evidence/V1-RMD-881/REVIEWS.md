# V1-RMD-881 incelemeleri

## qa-entegrasyon-cihaz
- `gonderenEtiket` "pasiftir" yorumu başka bir örneğe ait: düzeltildi (belgeGonderExt örneğinde boş eleman; yorum satırı raw:2265 gelen belge listeleme).
- `belgeGonderMsgOid` yerel değişken adı: not düzeltildi; `belgeOid` iddiası "doğrulanamadı" olarak kaldı.
- "Gereksiz kılmaz" sonucu aşırıydı: "ikinci dayanağı zayıflatır, QNB ürününün API'si doğrulanamadı" yapıldı.
- VUK/Kılavuz atfı: "bu görevde yeniden okunmadı, V13-FSC-005 notundan aktarıldı" eklendi.
- Webhook kuralının yalnız Cloud için olup olmadığı: "doğrulanamadı" eklendi (GOV-005 ve docs).
- Kabul edilmedi: tırnaklı alıntı biçimi (düşük), V0-QNB-001 hız sınırı (bu görevin dışı, V1-RMD-584 kaydı).

## qa-dokuman-bagimlilik
- `QnbSoapClient.cs:95-96` yolu eksikti: tam evidence yolu yazıldı.
- V13-HUG-001 notsuz kalmıştı: plan kapsamı genişletilip not eklendi (HUG-003/004 zaten V1-RMD-584 notu taşıyor).
- `qr-relay-topology.md` "yasaklıyor" ifadesi gevşetildi ("yeni tünel rotası gerekir").
- allowlist dosyası: V1-RMD-880'den kalan 2 bayat girdi silme; consistency-audit sıfır ihlal için gerekli, kapsama alındı.
- Kabul edilmedi: docs ana metnine "bkz. not" işareti (yalnız ekleme kuralı, silinen satır 0).

## Açık, kaynaklanamayan
- V13-GOV-003 (bölünmüş hesap ADR'si): repodan kaynaklı çelişki bulunamadı; Token'ın "iptal entegrasyondan yapılamaz" bulgusu ADR'nin kararını (ALKAROS N ayrı sepet gönderir) değiştirmez, yalnız iade yolunu etkiler (V13-HUG-003 notunda var).
- V13-GOV-006/008: polling/webhook/Cloud ifadesi yok, not gerekmedi.
