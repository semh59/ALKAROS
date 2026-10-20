# V1-RMD-644 - Denetim temizleyicisi sağlamlaştırması

Kaynak: V1-RMD-595 bulguları F02-F08, F19 (denetim kanıtında V1-RMD-640 adıyla).

- F02: yinelenen anahtarlı JSON artık çökmez; metin temizleme yoluna düşer.
- F03: AuditEvent zamanı UTC'ye çevrilir.
- F04: derlenmiş Regex nesnesi tek (statik); eskiden her değer için 39 yeni nesne.
- F05: anahtar listesi çoğul ve eş anlamlılarla genişledi (passwords, tokens, pwd, otp, iban, authorization, PINCode, JWTToken, cardNo, privateKey, accessToken vb.).
- F06: değer taraması artık anahtar adını bütün olarak değerlendirir ("Japan:", "span=" redakte edilmez).
- F07: kök düz metin taranır.
- F08: korunan anahtardan sonra tırnaksız değer satır sonuna veya ayırıcıya kadar maskelenir (Bearer, çok sözcüklü).
- F19: alan uzunlukları (128/64/32/128/128) kayıt öncesi doğrulanır.

Kanıt: red.log (27 test kırmızı), test-runs.log (95/95 yeşil, temiz derleme), mutations.log (53 mutant, 53 öldü, dosyalar orijinalle aynı). İlk mutasyon turlarında 6 mutant yaşadı; testler sıkılaştırılarak öldürüldü. Support DiagnosticBundle testleri 22/22.
Ders: mutasyon sonrası artımlı derleme eski ikiliyi kullanabilir; kapanış koşusu `dotnet build --no-incremental` ile yapıldı.
