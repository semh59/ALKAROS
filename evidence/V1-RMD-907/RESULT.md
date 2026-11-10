# V1-RMD-907 sonucu

Yöneticinin ikram/indirim/iptal taleplerine verdiği onay ve ret kararları artık denetim kaydına yazılıyor: authorization.grant.approved / authorization.grant.denied (aktör onaylayan, gerekçe talebin gerekçe kodu; durum, izin kodu, tutar, konu tipi ve kimliği, talep eden, yol). Daha önce karar yalnızca grant satırında (onaylayan ve zaman) kalıyordu.

## Kanıt
- Kırmızı (red-1.log): onay/ret sonrası denetim olayı yoktu.
- Yeşil: Authorization.Tests 15/15 (green-final.log), Identity.Authorization.Tests (identity-tests.log), Composition.Tests (composition.log).
- Mutantlar (mutations.txt): 6 öldü.
- Reddedilen karar (zaten çözülmüş) olay yazmaz: ARefusedDecisionLeavesNoAuditEvent.

## Açık
- Karar ve olay tek işlemde yazılır (grant deposuna ResolveAsync işlem aşırı yüklemesi eklendi): olay yazılamazsa karar verilmez, istemci yeniden dener. Olay gerekçesi talebin gerekçe kodudur (kararı verenin gerekçesi yok).
- Delegasyon oluşturma/geri alma ve sıkılaştırma temizleme hâlâ denetim olayı yazmıyor.
