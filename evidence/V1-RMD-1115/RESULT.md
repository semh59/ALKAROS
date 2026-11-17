# V1-RMD-1115 sonuc
Dogrulama (kod): CashierModifierPicker Escape'i yalniz arka plan div'inin onKeyDown'unda dinliyordu; odak pencere icinde yazi/bosluga tiklanip body'ye dusunce Escape islemiyordu: alarm kismen gercek (kirmizi test). Diger urunlerin tiklanamamasi modal tasarimdir (aria-modal, arka plana basinca pencere kapanir, test kilitlendi): duzeltme gerekmedi. Odak kapanista acan ogeye donuyordu (test kilitlendi).
Duzeltme: Escape window keydown dinleyicisine tasindi (pencere acikken, odak nerede olursa olsun kapatir); odak tuzagi (Tab) ve aria-modal ayni.
Kanit: red.log 1 kirmizi; green.log 7 test yesil; Cashier/workspace paketi 25 dosya/234 test yesil (green-full.log); mutations.txt M1-M4 killed; client-build-gate.log exit 0.
Acik risk: Escape window'da dinlendigi icin ayni anda baska bir pencere acik olsaydi ikisi birden kapanirdi; kasiyer ekraninda secenek penceresi acikken baska pencere acilmiyor (arka plan modal).
