import { useEffect, useState, type FormEvent } from "react";
import { invoiceSettingsText as t } from "../../strings";
import { loadSellerProfile, saveSellerProfile, type SellerProfile } from "./sellerProfileApi";

const empty: SellerProfile = { legalName: "", taxIdKind: "Vkn", taxIdNumber: "", taxOffice: "", address: "", district: "", city: "", email: null };

/** The business details printed on every invoice (legal name, tax number, tax office, address). */
export function SellerProfileCard({ terminalId, fetcher = fetch }: { terminalId: string; fetcher?: typeof fetch }) {
  const [profile, setProfile] = useState<SellerProfile>(empty);
  const [configured, setConfigured] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState("");
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    let current = true;
    loadSellerProfile(terminalId, fetcher)
      .then((loaded) => { if (current && loaded) { setProfile(loaded); setConfigured(true); } })
      .catch((reason: Error) => { if (current) setProblem(reason.message); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [terminalId, fetcher]);

  const change = (patch: Partial<SellerProfile>) => { setSaved(false); setProfile((previous) => ({ ...previous, ...patch })); };

  const save = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setProblem("");
    setSaved(false);
    try {
      await saveSellerProfile(terminalId, { ...profile, email: profile.email?.trim() ? profile.email.trim() : null }, fetcher);
      setConfigured(true);
      setSaved(true);
    } catch (reason) {
      setProblem((reason as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="login-card" aria-label={t.heading} aria-busy={loading || busy}>
      <div className="login-card-heading">
        <span>{t.kicker}</span>
        <h2>{t.heading}</h2>
        <p>{loading ? t.loading : configured ? t.configured : t.notConfigured}</p>
      </div>
      <form onSubmit={save}>
        <label>{t.legalName}<input value={profile.legalName} onChange={(event) => change({ legalName: event.target.value })} autoComplete="off" /></label>
        <label>{t.taxIdKind}
          <select value={profile.taxIdKind} onChange={(event) => change({ taxIdKind: event.target.value as SellerProfile["taxIdKind"] })}>
            <option value="Vkn">{t.vkn}</option>
            <option value="Tckn">{t.tckn}</option>
          </select>
        </label>
        <label>{t.taxIdNumber}<input value={profile.taxIdNumber} onChange={(event) => change({ taxIdNumber: event.target.value })} autoComplete="off" inputMode="numeric" /></label>
        <label>{t.taxOffice}<input value={profile.taxOffice} onChange={(event) => change({ taxOffice: event.target.value })} autoComplete="off" /></label>
        <label>{t.address}<input value={profile.address} onChange={(event) => change({ address: event.target.value })} autoComplete="off" /></label>
        <label>{t.district}<input value={profile.district} onChange={(event) => change({ district: event.target.value })} autoComplete="off" /></label>
        <label>{t.city}<input value={profile.city} onChange={(event) => change({ city: event.target.value })} autoComplete="off" /></label>
        <label>{t.email}<input type="email" value={profile.email ?? ""} onChange={(event) => change({ email: event.target.value })} autoComplete="off" /></label>
        {problem && <div className="alert error" role="alert">{problem}</div>}
        {saved && <div className="alert information" role="status">{t.savedMessage}</div>}
        <button className="primary login-submit" disabled={busy || loading}>{busy ? t.saving : t.save}</button>
      </form>
    </section>
  );
}
