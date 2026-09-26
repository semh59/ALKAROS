# Yemeksepeti webhook gizli değeri — kurulum notu

> **Doğrulanmamış taslak.** Bu bilgiler yalnız herkese açık Partner API v2.0.2 belgesine ve POS
> partner-picking SSS'sine dayanır. Gerçek bir sandbox teslimatıyla denenmedi (V0-YSP-001 `Blocked`, V20-INT-003).

## Ortam değişkeni

`ALKAROS_SECRET_YEMEKSEPETI_WEBHOOK_SECRET` değeri, Yemeksepeti'nin webhook isteğinde gönderdiği `Authorization`
başlığının **tamamıdır**. Partner Portal'da tanımladığınız değer neyse, başındaki şemayla birlikte aynen yazılır:

- Portal "Bearer" belirteci veriyorsa değer `Bearer <belirteç>` olur.
- Portal "Basic" kimlik bilgisi veriyorsa değer `Basic <base64>` olur.
- Şema yoksa değer yalnız belirteçtir.

Değişken yoksa kanal kapalıdır: uç `503 CHANNEL_NOT_CONFIGURED` döner, hiçbir şey okunmaz ve saklanmaz.

## Karşılaştırma nasıl yapılır (V12-RMD-007)

- İstek, gövdesi okunmadan önce doğrulanır. Doğrulanamayan istek, gövdesi ne kadar büyük olursa olsun
  `401 UNAUTHENTICATED` alır.
- Hem gelen değerde hem ayarlı değerde şema varsa şema büyük/küçük harfe duyarsız karşılaştırılır (`bearer` ile
  `Bearer` aynıdır, RFC 7235). Kimlik bilgisinin kendisi birebir ve sabit zamanlı karşılaştırılır.
- Yalnız birinde şema varsa değerler eşleşmez. Bu en sık yapılan ayar hatasıdır: Portal `Bearer abc` gönderirken
  değişkene yalnız `abc` yazılmışsa her teslimat 401 alır.

## Belirti ve çözüm

| Belirti | Olası neden |
| --- | --- |
| Her teslimat 401 | Değişkende şema eksik ya da fazla; ya da belirteç Portal'dakiyle aynı değil. |
| Her teslimat 503 | Değişken tanımlı değil; kanal kapalı. |
| Sağlayıcı 5 denemeden sonra vazgeçiyor | 401 veya 503 düzeltilmeden teslimatlar kaybolur; ayarı düzeltip Portal'dan yeniden gönderim isteyin. |
