# V1-GOV-125 - Garson ekranı denetim kaydı

- Task ID: V1-GOV-125
- Status: Done
- Assignee: Claude Opus 5
- Work type: documentation
- Surface state: Planned

## Goal

2026-09-10'da Garson ekranı için beş bağımsız ajanla yapılan denetimin
(backend, frontend, API uç noktaları, mimari sınırlar, veritabanı) bulgu
kaydını depoya yazar: ne bulundu, ne kapandı, ne açık kaldı ve neden.

Denetimin kendisi bir oturumda yapıldı ve sonuçları yalnız konuşmada duruyor.
`docs/engineering/v1-independent-audit.md`'nin kurduğu kalıp bu: bulgu kaydı
depoda yaşar, kapanan her madde hangi görevle kapandığını söyler. Kayıt
olmadan açık kalan 1 Critical ve 5 High'ı bir sonraki oturumun bulması
tesadüfe kalır.

## Owned surface

- `plan/v1/governance/V1-GOV-125-garson-audit-register.md` (yeni)
- `docs/engineering/garson-audit-2026-09-10.md` (yeni)

## In scope

1. Beş raporun bulgularının tek bir kayıtta toplanması, ajan ve şiddet
   bilgisiyle.
2. Her maddenin durumu: kapandıysa hangi görevle, açıksa niçin bırakıldığı.
3. Denetim sırasında değil, düzeltme sırasında bulunan ve hiçbir raporda
   olmayan kusurların ayrıca kaydı.
4. Ajanların çakışan bulgularının işaretlenmesi (ham sayı ile benzersiz
   sayının farkı).

## Out of scope

- Açık kalan bulguların düzeltilmesi: her biri kendi görevinde.
- Diğer modüllerin (Mutfak, Kasa, QR) denetimi.

## Dependencies

- V1-RMD-155

## Acceptance evidence

- `docs/engineering/garson-audit-2026-09-10.md` yazıldı: 84 ham bulgu, 20
  kapandı, 64 açık; her kapanan madde hangi görevle kapandığını söylüyor.
- Açık kalanın ağırlığı sayıyla değil şiddetle veriliyor: **1 Critical, 5
  High**, ve neredeyse tamamı veritabanı raporunda.
- Ajanların çakışan bulguları *(çakışan)* diye işaretlendi, ham sayı ile
  benzersiz sayının farkı açıkça söylendi.
- Denetimde olmayan 4 kusur ayrıca kaydedildi — üçü kendi işimi kontrol
  ederken çıktı, biri (mutfak sevkiyatçısının ikinci turu atlaması) testin
  kendisi yakaladı.
- Duran iki yapısal eksik (hiçbir siparişin kapanmaması, ödemenin olmaması)
  bulgu listesinden ayrı bir başlıkta.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: yeni ihlal yok.

## Handoff

- None
