import { useMemo, useState } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { formatMoney } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { errorText } from "../management/panel";
import { createChannelReportClient, type ChannelReportClient } from "./api";
import { dayCount, formatDay, isoDate, maxDays, providerLabel, sourceLabel, totalsOf, type ChannelFilter, type ChannelReport } from "./models";
import "./management-channel-report.css";

const t = managementText.channelReport;

const defaultRange = () => {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 6);
  return { from: isoDate(from), to: isoDate(to) };
};

export function ChannelReportSection({ client }: Partial<ManagementSectionProps> & { client?: ChannelReportClient }) {
  const api = useMemo(() => client ?? createChannelReportClient(), [client]);
  const [range, setRange] = useState(defaultRange);
  const [source, setSource] = useState<ChannelFilter>("");
  const [report, setReport] = useState<ChannelReport>();
  const [problem, setProblem] = useState<string>();
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setProblem(undefined);
    if (range.from === "" || range.to === "" || range.to < range.from) return setProblem(t.invalidRange);
    if (dayCount(range.from, range.to) > maxDays) return setProblem(t.tooLong);
    setLoading(true);
    try {
      setReport(await api.report(range.from, range.to, source));
    } catch (reason) {
      setReport(undefined);
      setProblem(errorText(reason, t.loadFailed));
    } finally {
      setLoading(false);
    }
  };

  const totals = report ? totalsOf(report.days) : undefined;

  return <div className="mcr">
    <form className="mcr__filters" onSubmit={(event) => { event.preventDefault(); void load(); }}>
      <TextField id="mcr-from" label={t.from} type="date" value={range.from} onChange={(event) => setRange({ ...range, from: event.target.value })} />
      <TextField id="mcr-to" label={t.to} type="date" value={range.to} onChange={(event) => setRange({ ...range, to: event.target.value })} />
      <SelectField id="mcr-source" label={t.channel} value={source} onChange={(event) => setSource(event.target.value as ChannelFilter)}>
        <option value="">{t.allChannels}</option>
        <option value="Qr">{t.qr}</option>
        <option value="Online">{t.online}</option>
      </SelectField>
      <Button type="submit" variant="secondary" disabled={loading}>{t.load}</Button>
    </form>
    {problem && <p role="alert" className="mcr__warning">{problem}</p>}
    {loading && <p aria-busy="true">{t.loading}</p>}
    {!report && !loading && !problem && <p className="mcr__hint">{t.hint}</p>}
    {report && !loading && <section aria-label={t.heading}>
      <h3>{t.heading}</h3>
      {!report.check.isBalanced && <p role="alert" className="mcr__warning">{t.unbalanced}</p>}
      {report.days.length === 0 ? <p className="mcr__empty">{t.empty}</p> : <table>
        <thead><tr>
          <th>{t.date}</th><th>{t.channel}</th><th>{t.platform}</th>
          <th className="mcr__num">{t.received}</th><th className="mcr__num">{t.accepted}</th><th className="mcr__num">{t.rejected}</th>
          <th className="mcr__num">{t.cancelled}</th><th className="mcr__num">{t.refused}</th>
          <th className="mcr__num">{t.gross}</th><th className="mcr__num">{t.net}</th></tr></thead>
        <tbody>{report.days.map((day) => <tr key={`${day.businessDate}-${day.source}-${day.provider ?? ""}`}>
          <td>{formatDay(day.businessDate)}</td><td>{sourceLabel(day.source)}</td><td>{providerLabel(day.provider)}</td>
          <td className="mcr__num">{day.ordersReceived}</td><td className="mcr__num">{day.accepted}</td><td className="mcr__num">{day.rejected}</td>
          <td className="mcr__num">{day.cancelled}</td><td className="mcr__num">{day.providerRefused}</td>
          <td className="mcr__num">{formatMoney(day.acceptedValue)}</td><td className="mcr__num">{formatMoney(day.acceptedNetValue)}</td></tr>)}</tbody>
        {totals && <tfoot><tr>
          <td colSpan={3}>{t.total}</td>
          <td className="mcr__num">{totals.received}</td><td className="mcr__num">{totals.accepted}</td><td className="mcr__num">{totals.rejected}</td>
          <td className="mcr__num">{totals.cancelled}</td><td className="mcr__num">{totals.refused}</td>
          <td className="mcr__num">{formatMoney(totals.acceptedValue)}</td><td className="mcr__num">{formatMoney(totals.acceptedNetValue)}</td></tr></tfoot>}
      </table>}
      <p className="mcr__hint">{t.notRevenue}</p>
    </section>}
  </div>;
}
