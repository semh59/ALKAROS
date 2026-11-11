# V1-RMD-953 Reviews

## qa-ux-tasarim
- Medium: the refund panel only shows while a bill is open on the Hesap screen, so "Hesap ekranından onaylanır" was incomplete -> the sentence now says to open the related bill on the Hesap screen.
- Low: the note shows to everyone who can open this screen (managers only) -> no harm.

## qa-test-kalitesi
- Low: the note stays visible in the empty state -> assert added. Loading/error states return early and show no note (accepted: nothing to point from).
- Low: the target screen is not checked by a contract test (out of this slice).
