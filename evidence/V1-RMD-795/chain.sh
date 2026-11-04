S=src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
SP=tests/Clients/StaticApps
R=src/Clients/PosTerminal/src/features/billing
M=evidence/V1-RMD-795/mutate.sh
sh $M m1-eft-slip $S "completing && completing.method === 'Eft' ? null :" "false ? null :" $SP "split-payment-refunds"
sh $M m2-eft-label $S "Eft: 'Havale / EFT', Account: 'Cari hesap' };
" "Eft: 'EFT', Account: 'Cari hesap' };
" $SP "split-payment-refunds" 
sh $M m3-selfapproved $S "row.selfApproved ? " "false ? " $SP "split-payment-refunds"
sh $M m4-short-cash $S "tendered < Number(state.amountDraft) - 0.004;" "false;" $SP "split-payment-change"
sh $M m5-timeout $S "setTimeout(function () { controller.abort(); }, ms);" "void 0;" $SP "client-timeout-fallback"
sh $M m6-raw-server-text $S "if (code && ERROR_TEXTS[code]) return ERROR_TEXTS[code];" "if (body && body.error && body.error.message) return body.error.message;" $SP "split-payment-failures"
sh $M m7-api-eft-slip $R/RefundPanel.tsx "client.complete(row.requestId, null)" "client.complete(row.requestId, '')" src/Clients/PosTerminal "RefundPanel"
sh $M m8-terminal-id $R/refundApi.ts "{ cashierUserId, cashierTerminalId, amount }" "{ cashierUserId, amount }" src/Clients/PosTerminal "refundApi"
sh $M m9-selfapproved-panel $R/RefundPanel.tsx "{row.selfApproved && " "{false && " src/Clients/PosTerminal "RefundPanel"
