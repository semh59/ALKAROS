#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-793/mutate.sh
R=src/Host/Experience/Billing/PaymentRefunds.cs
sh $M m12b-legacy-uses-any-till $R 'request.RequestedBy, cancellationToken)
                : null;' 'request.RequestedBy, cancellationToken)
                : (Guid?)await new NpgsqlCommand("SELECT cash_session_id FROM cash.cash_sessions WHERE cashier_user_id = @u AND status = '"'"'Open'"'"' LIMIT 1;", connection, transaction) { Parameters = { new NpgsqlParameter("u", request.RequestedBy) } }.ExecuteScalarAsync(cancellationToken);' tests/Host/Experience/Billing "FullyQualifiedName~ARequestFromBeforeTheTerminal"
echo done > evidence/V1-RMD-793/chain4.done
