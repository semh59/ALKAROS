# V1-RMD-028 autonomous browser transcript

- Surface: actual `TableWorkspace` composition with the production `FloorPlanWorkspace` component.
- URL: `http://127.0.0.1:58328/` from the task-owned Vite harness.
- Viewports: 17 desktop, tablet, mobile and breakpoint-boundary cases in `browser-matrix.json`.
- Reflow: 960 CSS pixels represented 200% equivalent reflow and 480 CSS pixels represented 400% equivalent reflow.
- Overflow: zero horizontal document overflow in every case.
- Targets: zero visible `button`, `input` or `select` elements below 44 pixels in width or height.
- Responsive switch: spatial canvas remained at 768 pixels; the dense accessible list replaced it at 767 pixels.
- Operation action: `Masa değiştir` opened the actual named action dialog.
- Modal focus: initial focus moved to the dialog close button; `Escape` closed the dialog and focus returned to the
  exact `Masa değiştir` trigger.
- Keyboard setup: `ArrowRight` changed A-01 X from 108 to 116, `R` changed rotation from 0 to 90 degrees, and the
  save changed the authoritative plan version from 6 to 7.
- Seat anchoring: the first seat retained `left: 0%; top: 0%` while its table moved, proving that table and seat
  coordinates translated together.
- Save: `Salon planı atomik olarak kaydedildi.` appeared after the versioned full-plan request.
- Console: zero browser error entries after the full matrix and interaction pass.
- Screenshots: `browser/*-final-top.png`, `browser/1920x1080-setup-viewport.png`, and the full viewport matrix.

Reservation claim/cancel remain visibly fail-closed when the server omits the reservation row version. A
merge-primary table shows `Birleşimi ayır` disabled if the server does not publish `Unmerge`; the client route is
implemented and becomes enabled only when the server explicitly authorizes that command.
