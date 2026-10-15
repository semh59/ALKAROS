# V1-RMD-560 kanıtı

Gerçek Kestrel sunucusunda, üretimdeki kayıt zinciriyle (`AddApiErrorHandling` + `AddServerErrorDetection` + `UseDualScreenErrorHandling`) iki gerçek HTTP isteği:

1. 500 atan uç nokta: istemci 500 ve standart hata zarfını alır; `api.unhandled_exception` tam 1 artar (RealServerErrorIsRecordedOnceAndClientStillGetsTheStandardEnvelope).
2. 404 atan uç nokta: hiçbir olay eklenmez (RealClientErrorProducesNoEvent).

Mutasyon: gözlemci `Insert(0)` yerine sona eklenirse, kaydı kaldırılırsa veya 5xx eşiği bozulursa test başarısız olur (3/3 öldürüldü, `mutations.log`). Bu, V1-RMD-534 denetiminin "kayıt sırası testsiz" bulgusunu kapatır.

Sınır: sunucu, DualScreenApplication.Build'in tamamı değil; yalnız hata hattı kayıtları ve ara katman kullanıldı. Denetim ve uyarı hizmetleri sahte (DispatchProxy), olay yazımı gerçek Postgres'e.
