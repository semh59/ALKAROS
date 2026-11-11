# V1-RMD-978 sonuc

Sorun: masada gonderilmis (Submitted) siparis varken kasiyer "Siparisse devam et" deyince 409 "Table already has a non-editable order." aliyordu (red.log).

Duzeltme:
- StartOrderAsync gonderilmis masa siparisini de yeniden baglar (DualScreenStore.Orders.cs).
- AddItemAsync acik hesaba AddRound ile yeni tur ekler; kilit Draft ve Submitted siparisi kabul eder (DualScreenStore.cs).
- Order.ChangeItemQuantity / RemoveItem acik hesapta calisir; gonderilmis kalem yine degistirilemez (kalem durumu kurali).
- Ekran goruntusu: Editable yalniz gonderilmemis kalem varken, yeni AcceptsRound alani masa siparisi hesapsizken true (DualScreenStore.Display.cs, DualScreenContracts.cs, PosTerminal Cashier.tsx).

Testler: DualScreenStoreTests (yeni 5 test), CustomerDisplay testleri 37 gecti, OrderDomainTests 17 gecti, PosTerminal first-touch + stale 27 gecti, tsc temiz. Mutasyonlar mutations.txt (M1-M7 olu; M3 yalniz domain testinde).
Mutasyonlar: mutations.txt (3 mutant, 3 oldu; M3 host testinde esdeger, domain testinde olu).

Bilinen sinir: gonderilecek kalem yokken Siparisi gonder yalniz acik hesapta yeni kalem varken gorunur; Tahsil et yolu degismedi.
