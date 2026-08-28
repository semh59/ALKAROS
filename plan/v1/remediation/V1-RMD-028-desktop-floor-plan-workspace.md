# V1-RMD-028 - Desktop floor-plan workspace

- Task ID: V1-RMD-028
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

Reddedilen masa kartı `Harita` görünümünü masaüstü öncelikli mekânsal salon çalışma alanı ve açık kurulum/operasyon
modlarıyla değiştirmek; tablet/mobil ve erişilebilirlik için yoğun liste alternatifini korumak.

## Owned surface

- `src/Clients/PosTerminal/src/features/tables/**`
- `evidence/V1-RMD-028/**`

## Dependencies

- V1-RMD-026
- V1-RMD-016
- V1-RMD-025

## Acceptance evidence

- Salon; authoritative bölge boyutlarını, masa geometrisini, şekli, sandalyeleri, metinsel durumu, geçen süreyi,
  sipariş/hesap/rezervasyon/birleştirme işaretlerini ve veri tazeliğini gösterir. Seçili masa denetçisi operasyon
  bağlamını görünür tutar.
- Kurulum modu oluşturma, seçme, klavyeyle taşıma/boyutlandırma/döndürme, pointer ile sürükleme, sandalye düzenleme,
  doğrulama incelemesi, atomik kayıt ve conflict recovery sağlar. Operasyon modu geometriyi yanlışlıkla değiştiremez.
- Taşıma, birleştirme, ayırma, rezervasyon ve yaşam döngüsü eylemleri masa bağlamında görünürdür ve yalnız sunucunun
  izin verdiği komutlarla etkinleşir. Busy/error/offline/stale/unauthorized/conflict durumları seçimi ve taslağı korur.
- Component testleri, tüm PosTerminal testleri, typecheck ve evidence kapsamlı build exit code `0` verir. Masaüstü,
  tablet, mobil, breakpoint ve yüzde 200/400 eşdeğer reflow otonom tarayıcı kanıtında overflow, kesilmiş kritik eylem
  veya 44x44 altı hedef bulunmaz; klavyeyle tamamlama ve focus restoration geçer.

## Handoff

- V1-RMD-029
