# V1-RMD-1136 sonuc
Ayar duzenleyicisine "metin" kurali: cash.drawer_printer_address icin istemci, sunucuyla ayni host:port kuralini (bos serbest, port 1-65535, etiket kurali, 253 sinir, kirpma) uygular ve Turkce ipucunu gosterir; cash.drawer_kick_enabled acma/kapama ve "denemeden acmayin" uyarisi gorunur. docs/qa/device-browser-test-plan.md'ye "Kasa cekmecesi (gercek cihaz)" bolumu: ilk satir uyari, 10 adim + beklenen sonuc, sorun giderme, bilinen sinirlar.
Kanit: red.log (9 kirmizi), green.log, green-project.log (PosTerminal 126 dosya/1516 test), mutations.txt M1-M12 killed, clientBuildGate exit 0, plan/consistency audit temiz.
Acik risk: kilavuz adimlari gercek cihazda Semih tarafindan denenecek; sunucu reddi icin metin ayarina ozel test yok (genel testler kapsar).
