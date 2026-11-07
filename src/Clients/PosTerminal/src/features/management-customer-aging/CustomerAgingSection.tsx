import { useCallback, useMemo } from "react";
import { Button } from "../../design-system";
import { formatMoney } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { PanelView, usePanel } from "../management/panel";
import { createCustomerAgingClient, type CustomerAgingClient } from "./api";
import { formatDay, totalsOf } from "./models";
import "./management-customer-aging.css";

const t = managementText.customerAging;
const past = (value: number | null) => value === null ? t.noSnapshot : formatMoney(value);

export function CustomerAgingSection({ client }: Partial<ManagementSectionProps> & { client?: CustomerAgingClient }) {
  const api = useMemo(() => client ?? createCustomerAgingClient(), [client]);
  const load = useCallback(() => api.report(), [api]);
  const [panel, reload] = usePanel(load, t.loadFailed);

  return <div className="mca">
    <div className="mca__toolbar">
      <Button data-telemetry-id="b.management-customer-aging.customeragingsection.1" variant="secondary" onClick={reload}>{t.refresh}</Button>
    </div>
    <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={panel} onRetry={reload}>
      {(report) => {
        const totals = totalsOf(report.rows);
        return <>
          <p className="mca__hint">{t.asOf(formatDay(report.asOf))}</p>
          {!report.check.isBalanced && <p role="alert" className="mca__warning">{t.unbalanced}</p>}
          {report.rows.length === 0 ? <p className="mca__empty">{t.empty}</p> : <table>
            <thead><tr>
              <th>{t.customer}</th>
              <th className="mca__num">{t.bucket30}</th><th className="mca__num">{t.bucket60}</th>
              <th className="mca__num">{t.bucket90}</th><th className="mca__num">{t.bucketOlder}</th>
              <th className="mca__num">{t.totalDebt}</th><th className="mca__num">{t.credit}</th>
              <th className="mca__num">{t.termDays}</th><th className="mca__num">{t.overdue}</th>
              <th className="mca__num">{t.ago30}</th><th className="mca__num">{t.ago60}</th><th className="mca__num">{t.ago90}</th></tr></thead>
            <tbody>{report.rows.map((row) => <tr key={row.customerId}>
              <td>{row.name}</td>
              <td className="mca__num">{formatMoney(row.days0To30)}</td><td className="mca__num">{formatMoney(row.days31To60)}</td>
              <td className="mca__num">{formatMoney(row.days61To90)}</td><td className="mca__num">{formatMoney(row.over90)}</td>
              <td className="mca__num">{formatMoney(row.totalDebt)}</td><td className="mca__num">{formatMoney(row.unappliedCredit)}</td>
              <td className="mca__num">{row.paymentTermDays ?? t.noSnapshot}</td><td className="mca__num">{row.overdueAmount === null ? t.noSnapshot : formatMoney(row.overdueAmount)}</td>
              <td className="mca__num">{past(row.balance30DaysAgo)}</td><td className="mca__num">{past(row.balance60DaysAgo)}</td><td className="mca__num">{past(row.balance90DaysAgo)}</td></tr>)}</tbody>
            <tfoot><tr>
              <td>{t.total}</td>
              <td className="mca__num">{formatMoney(totals.days0To30)}</td><td className="mca__num">{formatMoney(totals.days31To60)}</td>
              <td className="mca__num">{formatMoney(totals.days61To90)}</td><td className="mca__num">{formatMoney(totals.over90)}</td>
              <td className="mca__num">{formatMoney(totals.totalDebt)}</td><td className="mca__num">{formatMoney(totals.unappliedCredit)}</td><td className="mca__num">{t.noSnapshot}</td><td className="mca__num">{totals.overdueAmount === null ? t.noSnapshot : formatMoney(totals.overdueAmount)}</td><td colSpan={3}>{t.noSnapshot}</td></tr></tfoot>
          </table>}
          <p className="mca__hint">{t.method}</p>
        </>;
      }}
    </PanelView>
  </div>;
}
