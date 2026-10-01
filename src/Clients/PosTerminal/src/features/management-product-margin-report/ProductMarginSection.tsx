import { useMemo, useState } from "react";
import { Button, TextField } from "../../design-system";
import { formatMoney } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { errorText } from "../management/panel";
import { dayCount, isoDate, maxDays } from "../management-channel-report/models";
import { createProductMarginClient, type ProductMarginClient } from "./api";
import { formatPercent, formatQuantity, type ProductMarginReport } from "./models";
import "./management-product-margin-report.css";

const t = managementText.productMargin;

const defaultRange = () => {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 6);
  return { from: isoDate(from), to: isoDate(to) };
};

export function ProductMarginSection({ client }: Partial<ManagementSectionProps> & { client?: ProductMarginClient }) {
  const api = useMemo(() => client ?? createProductMarginClient(), [client]);
  const [range, setRange] = useState(defaultRange);
  const [report, setReport] = useState<ProductMarginReport>();
  const [problem, setProblem] = useState<string>();
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setProblem(undefined);
    if (range.from === "" || range.to === "" || range.to < range.from) return setProblem(t.invalidRange);
    if (dayCount(range.from, range.to) > maxDays) return setProblem(t.tooLong);
    setLoading(true);
    try {
      setReport(await api.report(range.from, range.to));
    } catch (reason) {
      setReport(undefined);
      setProblem(errorText(reason, t.loadFailed));
    } finally {
      setLoading(false);
    }
  };

  return <div className="mpm">
    <form className="mpm__filters" onSubmit={(event) => { event.preventDefault(); void load(); }}>
      <TextField id="mpm-from" label={t.from} type="date" value={range.from} onChange={(event) => setRange({ ...range, from: event.target.value })} />
      <TextField id="mpm-to" label={t.to} type="date" value={range.to} onChange={(event) => setRange({ ...range, to: event.target.value })} />
      <Button type="submit" variant="secondary" disabled={loading}>{t.load}</Button>
    </form>
    {problem && <p role="alert" className="mpm__warning">{problem}</p>}
    {loading && <p aria-busy="true">{t.loading}</p>}
    {!report && !loading && !problem && <p className="mpm__hint">{t.hint}</p>}
    {report && !loading && <section aria-label={t.heading}>
      <h3>{t.heading}</h3>
      {!report.check.isBalanced && <p role="alert" className="mpm__warning">{t.unbalanced}</p>}
      {report.unknownCostLines > 0 && <p role="alert" className="mpm__warning">{t.unknownWarning} {report.unknownCostLines}</p>}
      {report.rows.length === 0 ? <p className="mpm__empty">{t.empty}</p> : <table>
        <thead><tr>
          <th>{t.product}</th><th className="mpm__num">{t.sold}</th><th className="mpm__num">{t.givenAway}</th>
          <th className="mpm__num">{t.revenue}</th><th className="mpm__num">{t.cost}</th>
          <th className="mpm__num">{t.margin}</th><th className="mpm__num">{t.marginPercent}</th></tr></thead>
        <tbody>{report.rows.map((row) => <tr key={row.productId}>
          <td>{row.productName}</td><td className="mpm__num">{formatQuantity(row.soldQuantity)}</td><td className="mpm__num">{formatQuantity(row.givenAwayQuantity)}</td>
          <td className="mpm__num">{formatMoney(row.netRevenue)}</td>
          <td className="mpm__num">{formatMoney(row.cost)}{row.unknownCostLines > 0 && ` (${t.partial})`}</td>
          <td className="mpm__num">{row.grossMargin === null ? t.unknownCost : formatMoney(row.grossMargin)}</td>
          <td className="mpm__num">{row.marginPercent === null ? t.unknownCost : formatPercent(row.marginPercent)}</td></tr>)}</tbody>
        <tfoot><tr>
          <td colSpan={3}>{t.total}</td>
          <td className="mpm__num">{formatMoney(report.totalNetRevenue)}</td><td className="mpm__num">{formatMoney(report.totalCost)}</td>
          <td colSpan={2} className="mpm__num">{report.unknownCostLines > 0 ? t.unknownCost : formatMoney(report.totalNetRevenue - report.totalCost)}</td>
        </tr></tfoot>
      </table>}
      <p className="mpm__hint">{t.discountNote} {formatMoney(report.check.billLevelDiscounts)}</p>
      <p className="mpm__hint">{t.costNote}</p>
    </section>}
  </div>;
}
