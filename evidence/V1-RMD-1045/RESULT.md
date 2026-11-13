# V1-RMD-1045 RESULT

Karar (Semih 2026-11-13): onaylanmis ama fisi/havalesi girilmemis (AwaitingManualRefund) iade yonetici tarafindan iptal edilebilsin.
Duzeltme: mevcut POST refund-requests/{id}/reject (payments.refund.approve, zorunlu not, kendi karari kurali ayni) artik AwaitingManualRefund istegini de Rejected yapar; yeni durum/uç nokta/migration yok. Completed ve Rejected eskisi gibi 409 REFUND_STATE. Rejected olunca acik-iade engeli (HasOpenRefundRequest, 1037/1043) kalkar, hesap yeniden kapanir, ayni odemeye yeni iade istenebilir.
Kasa butcesinden (allowance) onayli iade iptal edilirse tutar, yalniz harcamanin yapildigi hala acik vardiyaya geri yazilir (opened_at <= onay zamani); yonetici onayli iadede butceye dokunulmaz. Denetim kaydi refund.rejected: onceki durum, onaylayan, onay zamani, butceden mi, butce geri yazildi mi.
Kanit: red.log (4 yeni test kirmizi), green.log Billing.Tests 367/368 (1 PG baglanti zaman asimi: KitchenlessAdd), green-rerun.log o test ve yeni testler 36/36; mutations.txt M1-M9 killed; task-scope exit 0.
Mevcut test guncellendi: RejectingARefundThatIsNoLongerRequested artik Completed isteği reddetmeyi dener (eskiden onayli ama tamamlanmamis istegi reddetmek 409 idi).
Elle senaryo: kartla ode, iade iste, yonetici onayla (Elle iade bekliyor); fis girmeden yonetici ret nedeniyle iptal eder (ekranlar 1046/1047); hesap yeniden kapanir.
Acik (urun karari): fiziksel iade yapildi ama sisteme girilmediyse iptal ayni odemenin ikinci kez iadesine yol acar; iz audit'te; ekranlar iptal onayinda bunu uyarir. Onaylayan kisi satirda decided_by olarak ezilir, onay audit'te kalir.
qa-backend: durum makinesi, kilit sirasi, vardiya/negatif butce temiz. Orta: onay bilgisi satirda eziliyor + audit eksik -> audit onceki durum/onaylayan/butce geri yazildi eklendi (M8, M9). Dusuk: ReleaseAsync sonucu -> bool doner, audit'e yazilir.
qa-guvenlik-yetki: kasiyer 403, kendi karari kurali, es zamanlilik, butce istismari temiz. Orta (ayni onay bilgisi) duzeltildi. Dusuk: cift fiziksel iade riski -> ekran iptal onayinda uyarir (1046/1047).
Dogrulama turu: duzeltmelerden sonra yeni testler + Kitchenless 36/36, M8/M9 killed.
