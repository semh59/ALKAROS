# V1-RMD-986 Result

The notice box no longer sits at the top over the header buttons. On a wide screen it is anchored to the bottom, inside the product area (left of the ticket panel, so it does not cover payment buttons). On a narrow screen it sits above the ticket bar including the safe area. Layout cannot be measured in jsdom: the test guards the placement rules; a real browser check at 1280x720, 1024x768 and a phone is still needed.

- red.log: placement test fails before; green.log: touchTargets 18 of 18 pass.
- mutations.txt: 2 mutations killed, files restored (cmp).
