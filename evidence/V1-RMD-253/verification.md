# V1-RMD-253 — Verification transcript

## Ortam
- Chromium 141.0.7390.37 (`/opt/pw-browsers/chromium-1194`, Playwright 1.56.1)
- Cashier statik dosyaları gerçek olarak sunuldu; `/api/v1/*` uç noktaları
  (session, catalog, orders/staff, table-draft, submit-draft, send-to-cashier)
  temsili örnek veri döndüren yerel bir stub sunucuyla yanıtlandı.

## Gerçek tarayıcı senaryosu
1. Ürün kartına tıkla (sepete ekle).
2. "Siparişi Onayla & Mutfağa İlet"e tıkla.
3. Playwright'in `page.on('dialog', ...)` dinleyicisi hiç tetiklenmedi
   (`dialogFired: false`) — native `alert()` artık hiç çağrılmıyor.
4. `#toastRegion` içinde gerçek bir toast belirdi:
   ```json
   {"text":"Sipariş mutfağa iletildi. (1 kalem, ₺185,00)","className":"toast toast--success","role":"status"}
   ```
5. ~5 saniye sonra, hiçbir tıklama olmadan toast kendiliğinden DOM'dan
   kaldırıldı (`self-dismissed: true`) — `toast-dismissed.png`.

## Bulunan ve düzeltilen bir yerleşim sorunu (şeffaflık için kayıtlı)

İlk denemede `.toast-region` sağ-alt köşede konumlandırılmıştı
(`right/bottom: 16px`) — bu, ekranın en sık kullanılan birincil eylem
butonunu ("SİPARİŞİ ONAYLA & MUTFAĞA İLET") kısmen ÖRTÜYORDU
(`toast-visible.png`'in ilk hali, artık üzerine yazıldı). Bu, aynı düzeltmenin
kendisinin yeni bir kullanılabilirlik sorunu yaratacağı anlamına geliyordu.
Konum `top: 68px; right: 16px` olarak değiştirildi (başlık çubuğunun hemen
altı) — birincil eylem butonu artık tamamen açık. Küçük, ikincil "Beklet /
Fişler / İptal" düğmeleriyle ~5sn'lik gösterim süresince kısmi bir görsel
kesişim kalıyor (bunlar dispatch sonrası nadiren hemen kullanılan eylemler);
bu, birincil eylemi kapatmaktan çok daha düşük bir maliyet olarak
değerlendirildi, ama tamamen mükemmel değil — gerekirse ayrı bir ince ayar
görevi açılabilir.

## Otomatik testler

```
$ cd tests/Clients/StaticApps && npx vitest run
 Test Files  3 passed (3)
      Tests  24 passed (24)
```

4 test dosyası `alert`/`vi.stubGlobal("alert", ...)` bekliyordu; hepsi
`#toastRegion .toast` DOM'unu okuyan bir `toastMessages()` yardımcısına
taşındı. Test sayısı ve kapsamı değişmedi (24/24).

## Proje-geneli kontroller

- `python tools/plan-audit/plan_audit_tool.py validate` → 1 hata
  (`C54_APPLICATION_ADMISSION_V3_FINAL_MISSING`), bu görevden önce de vardı,
  ilgisiz.
- `python tools/consistency-audit/consistency_audit.py` → `consistency-audit: clean`.

## Çalıştırılamayan kontrol

`dotnet build`/`dotnet test`: bu Claude Code oturumunda .NET SDK kurulu
değil (`dotnet: command not found`, V1-RMD-252'de de aynı durum). Owned
surface yalnızca statik dosyalar (HTML/CSS/JS + JS test) — hiçbir C#
değişmedi.
