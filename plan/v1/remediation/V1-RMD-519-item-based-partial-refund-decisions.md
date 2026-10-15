# V1-RMD-519 - Ürün bazlı kısmi iade (PO kararları ve zincir)

- Task ID: V1-RMD-519
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: planning
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Bugün iade yalnız ödemenin tamamı içindir. Ürün bazlı kısmi iade için PO kararları kayda geçer; uygulama görevleri bu kararlara göre ayrı açılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-519-item-based-partial-refund-decisions.md`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- PO kararları (2026-10-13): iade ürün bazlıdır; her iadede kasiyer ürün için "stoğa geri dön" seçimini yapar; birden fazla ödeme yöntemi olan hesapta kasiyer iadenin hangi ödemeden düşeceğini seçer; PO 2026-10-14 ile limit, iade başına değil vardiya başına iade bütçesidir: müdür her vardiya için tutarı verir, kasiyerin kendi tamamladığı iadeler bütçeden düşer, yetmeyen iade müdür onayına gider (V1-RMD-520 ve V1-RMD-521 bunu uygular; müdür bütçeyi bekleyen iade isteğinin yanından, isteği açan kasiyere verir; önceki "100 TL varsayılan limit" fikri geçersizdir).
- Bilinen çelişkiler: veritabanında iade isteğini açanın aynı kişi tarafından karara bağlanmasını yasaklayan kısıt vardır (`ck_refund_requests_decider_not_requester`); nakit iadede "kasiyer ister, müdür onaylar" kararı vardır (2026-10-13). Bütçe içinde kendi kendine tamamlama bu iki kuralı bilerek gevşetir; V1-RMD-520 bunu `approval_mode` ile açıkça ele alır.
- Doğrulanacaklar: stok geri alma servisinin kalem içinde kısmi miktarı destekleyip desteklemediği; sayısal işletme ayarı için kayıt yeri bulunmadığı (yeni tablo ve yönetim ekranı gerekebilir).

- PO 2026-10-14 (devam): adet seçilebilir (kalem içinden 1 adet); kısmi iadeden sonra hesap Ödendi kalır. Doğrulananlar: stok geri alma bugün yalnız tüm hareketi ve bir kez geri alır (kısmi adet için yeni Return hareketi gerekir, V1-RMD-523); kısmen iade edilmiş ödeme kapanış hesabında sayılmıyor (V1-RMD-522). Zincir: 522, 523, 524, 525, 526 (tamamlandı), 527 (kanal raporu iade netlemesi, tamamlandı), 528 (ürün marj raporu iade netlemesi, tamamlandı), 529 (kalemsiz iade ayrı satır, tamamlandı), 530 (rapor ekranlarında iade tutarları, tamamlandı); zincir kapandı; sonraki bağımsız işler 531, 532; izleme zinciri 533-557 (docs/MONITORING_SYSTEM_PLAN.md; 552 bekçi mantığı kapsamına daraltıldı; 538 iç kuyruk ve kanal arayüzü; 554 güvenlik dedektörleri; 555 sağlık görüntüsü; 559 denetim düzeltmeleri; 560 uçtan uca test; 561 bildirim teslimat bağlantısı; 562 gizlilik politikası; 546 veritabanı içgörü; 563 içgörü Host işi; 545 sentetik sonda; 536 sapma motoru; 564 sapma motoru Host işi; 537 tarayıcı hata raporlama).

## Out of scope

- Uygulama kodu; bu görev yalnız kararları ve zinciri kaydeder.

## Dependencies

- V1-RMD-508

## Acceptance evidence

- Uygulama görevleri açıldığında bu kayıt güncellenir.

## Handoff

- None
