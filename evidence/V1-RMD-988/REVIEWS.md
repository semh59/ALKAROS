# V1-RMD-988 Reviews

## qa-test-kalitesi
Applied: cancelled-bill condition of the third copy checked; copy count moved to its own test with a message.

## qa-veritabani
Condition identical in Billing, Tables and the three Reporting copies; every Host consumer uses BillOrderLink.Matches.
Open (separate follow-up, not changed here): src/Modules/Reporting/Channels/ChannelReportService.cs:78-84 groups refunds by b.order_id, so refunds of a merged bill (no order id) may be missed in the channel report if QR/online orders can join a merged bill (not verified). DualScreenStore.Display.cs:351 holds a reverse-direction variant (first order of a bill), intentionally different.
