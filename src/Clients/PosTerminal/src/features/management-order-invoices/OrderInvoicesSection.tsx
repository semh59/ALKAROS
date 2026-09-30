import { useMemo, useState } from "react";
import { Button, TextField } from "../../design-system";
import { formatMoney } from "../../format";
import { managementText } from "../../strings";
import { errorText } from "../management/panel";
import { dayCount, formatDay, isoDate, maxDays, providerLabel } from "../management-channel-report/models";
import type { ManagementSectionProps } from "../management/sections";
import { createOrderInvoicesClient, type OrderInvoicesClient } from "./api";
import { statusLabel, type OrderInvoiceDetail, type OrderInvoiceList } from "./models";
import "./management-order-invoices.css";

const t = managementText.orderInvoices;

const defaultRange = () => {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 6);
  return { from: isoDate(from), to: isoDate(to) };
};

export function OrderInvoicesSection({ client }: Partial<ManagementSectionProps> & { client?: OrderInvoicesClient }) {
  const api = useMemo(() => client ?? createOrderInvoicesClient(), [client]);
  const [range, setRange] = useState(defaultRange);
  const [list, setList] = useState<OrderInvoiceList>();
  const [detail, setDetail] = useState<OrderInvoiceDetail>();
  const [problem, setProblem] = useState<string>();
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setProblem(undefined);
    setDetail(undefined);
    if (range.from === "" || range.to === "" || range.to < range.from) return setProblem(t.invalidRange);
    if (dayCount(range.from, range.to) > maxDays) return setProblem(t.tooLong);
    setLoading(true);
    try {
      setList(await api.list(range.from, range.to));
    } catch (reason) {
      setList(undefined);
      setProblem(errorText(reason, t.loadFailed));
    } finally {
      setLoading(false);
    }
  };

  const open = async (orderId: string) => {
    setProblem(undefined);
    try {
      setDetail(await api.detail(orderId));
    } catch (reason) {
      setProblem(errorText(reason, t.detailFailed));
    }
  };

  return <div className="moi">
    <form className="moi__filters" onSubmit={(event) => { event.preventDefault(); void load(); }}>
      <TextField id="moi-from" label={t.from} type="date" value={range.from} onChange={(event) => setRange({ ...range, from: event.target.value })} />
      <TextField id="moi-to" label={t.to} type="date" value={range.to} onChange={(event) => setRange({ ...range, to: event.target.value })} />
      <Button type="submit" variant="secondary" disabled={loading}>{t.load}</Button>
    </form>
    {problem && <p role="alert" className="moi__warning">{problem}</p>}
    {loading && <p aria-busy="true">{t.loading}</p>}
    {!list && !loading && !problem && <p className="moi__hint">{t.hint}</p>}
    {list && !loading && <>
      {list.missing.length > 0 && <section aria-label={t.missingHeading}>
        <h3>{t.missingHeading}</h3>
        <p className="moi__warning">{t.missingExplain}</p>
        <table>
          <thead><tr><th>{t.order}</th><th>{t.platform}</th><th>{t.deliveredAt}</th><th className="moi__num">{t.gross}</th><th className="moi__num">{t.daysLeft}</th></tr></thead>
          <tbody>{list.missing.map((order) => <tr key={order.orderId}>
            <td>{order.orderNumber}</td><td>{providerLabel(order.provider)}</td><td>{formatDay(order.deliveredAt.slice(0, 10))}</td>
            <td className="moi__num">{formatMoney(order.total)}</td><td className="moi__num">{order.daysLeft}</td></tr>)}</tbody>
        </table>
      </section>}
      <section aria-label={t.heading}>
        <h3>{t.heading}</h3>
        {list.invoices.length === 0 ? <p className="moi__empty">{t.empty}</p> : <table>
          <thead><tr>
            <th>{t.serviceDate}</th><th>{t.order}</th><th>{t.platform}</th><th>{t.status}</th>
            <th className="moi__num">{t.net}</th><th className="moi__num">{t.tax}</th><th className="moi__num">{t.gross}</th><th>{t.detail}</th></tr></thead>
          <tbody>{list.invoices.map((invoice) => <tr key={invoice.invoiceId}>
            <td>{formatDay(invoice.serviceDate)}</td><td>{invoice.orderNumber}</td><td>{providerLabel(invoice.provider)}</td><td>{statusLabel(invoice.status)}</td>
            <td className="moi__num">{formatMoney(invoice.netAmount)}</td><td className="moi__num">{formatMoney(invoice.taxAmount)}</td>
            <td className="moi__num">{formatMoney(invoice.grossAmount)}</td>
            <td><Button variant="secondary" onClick={() => void open(invoice.orderId)}>{t.show}</Button></td></tr>)}</tbody>
        </table>}
        <p className="moi__hint">{t.notSent}</p>
      </section>
      {detail && <section className="moi__detail" aria-label={t.detailHeading}>
        <h3>{t.detailHeading}</h3>
        <p>{t.seller}: {detail.seller.legalName} — {detail.seller.taxOffice} — {detail.seller.taxIdNumber}</p>
        <p>{detail.seller.address}</p>
        <p>{t.webAddress}: {detail.webAddress}</p>
        <table>
          <thead><tr><th>{t.line}</th><th className="moi__num">{t.quantity}</th><th className="moi__num">{t.taxRate}</th><th className="moi__num">{t.gross}</th></tr></thead>
          <tbody>{detail.lines.map((line) => <tr key={line.lineNumber}>
            <td>{line.description}</td><td className="moi__num">{line.quantity}</td><td className="moi__num">%{line.taxRate}</td>
            <td className="moi__num">{formatMoney(line.grossAmount)}</td></tr>)}</tbody>
        </table>
      </section>}
    </>}
  </div>;
}
