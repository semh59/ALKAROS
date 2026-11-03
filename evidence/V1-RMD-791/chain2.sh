#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-791/mutate.sh; P=tests/Host/Experience/Reporting; Fl="FullyQualifiedName~EndOfDayServerFiguresTests"
R=src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs
sh $M m1-window src/Modules/Reporting/BusinessDayTotals/BusinessDayWindow.cs 'new(6, 0)' 'new(5, 0)' tests/Modules/Reporting/V1Operations "FullyQualifiedName~BusinessDayWindowTests"
sh $M m2-sentvoid $R "event_name IN ('Order.ItemVoided', 'Order.SentItemVoided')
                AND occurred_at" "event_name IN ('Order.ItemVoided')
                AND occurred_at" $P "$Fl"
sh $M m3-tipstatus $R "b.status = 'Paid' AND b.closed_at" "b.status <> 'Open' AND b.closed_at" $P "$Fl"
sh $M m4-revenue-tips $R '- (SELECT amount FROM tips),' '- 0,' $P "$Fl"
sh $M m5-printwindow $R '(SELECT COUNT(*) FROM kitchen.print_jobs WHERE failed_at >= $1 AND failed_at < $2)' '(SELECT COUNT(*) FROM kitchen.print_jobs WHERE failed_at >= $1)' $P "$Fl"
sh $M m6-recovered $R "AND j.status = 'Printed'" "AND j.status <> 'Printed'" $P "$Fl"
sh $M m7-ack src/Modules/Reporting/V1Operations/PostgresOperationalReportRepository.cs 'figures.OpenItems.Any && !acknowledgeOpenItems' 'figures.OpenItems.Any && false' $P "$Fl"
sh $M m8-auditname src/Host/Experience/Reporting/EndOfDayEndpoints.cs '"reporting.business_day_closed"' '"reporting.business_day_closedx"' $P "$Fl"
sh $M m9-ackflag src/Modules/Reporting/V1Operations/PostgresOperationalReportRepository.cs 'var acknowledged = figures.OpenItems.Any;' 'var acknowledged = true;' $P "$Fl"
sh $M m10-cashwindow $R 'FROM cash.cash_sessions WHERE closed_at >= $1 AND closed_at < $2);' 'FROM cash.cash_sessions WHERE closed_at >= $1);' $P "$Fl"
sh $M m11-openbills $R "WHERE status NOT IN ('Paid', 'Cancelled')),
            (SELECT COUNT(*) FROM payments" "WHERE status NOT IN ('Paid')),
            (SELECT COUNT(*) FROM payments" $P "$Fl"
sh $M m12-unsettled $R "status IN ('Pending', 'Unknown', 'ReconciliationRequired')" "status IN ('Pending', 'Unknown')" $P "$Fl"
sh $M m13-rollback src/Modules/Reporting/V1Operations/PostgresOperationalReportRepository.cs 'await inTransaction(closedDay, figures, connection, transaction, cancellationToken);' 'await Task.CompletedTask;' tests/Modules/Reporting/V1Operations "FullyQualifiedName~PostgresOperationalReportRepositoryTests"
echo done > evidence/V1-RMD-791/chain2.done
