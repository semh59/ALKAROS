import { useCallback, useEffect, useId, useState } from "react";
import { formatMoney } from "../../format";
import { platformLabel } from "../online-operations";
import {
  OnlineProblemsApiError,
  loadProblems,
  problemActionLabel,
  problemKindLabel,
  problemStatusLabel,
  resolveProblem,
  retryProblem,
  type OnlineProblem,
} from "./onlineProblemsApi";
import "./online-problems.css";

const when = (iso: string) =>
  new Date(iso).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
const failure = (error: unknown, fallback: string) => (error instanceof OnlineProblemsApiError ? error.message : fallback);

/**
 * V12-OUI-006: every open online order problem with its platform, order and safe next action in Turkish. Staff who see
 * reports read the list; a reconciliation manager retries the next action or closes the problem with a note — the
 * server refuses to close one whose source still disagrees.
 */
export function OnlineProblemsTab({ terminalId, canAct }: { terminalId: string; canAct: boolean }) {
  const [problems, setProblems] = useState<OnlineProblem[] | null>(null);
  const [loadError, setLoadError] = useState<string>();
  const [actionError, setActionError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [resolving, setResolving] = useState<OnlineProblem>();
  const [note, setNote] = useState("");
  const noteId = useId();

  const load = useCallback(async () => {
    try {
      setProblems(await loadProblems(terminalId));
      setLoadError(undefined);
    } catch (error) {
      setLoadError(failure(error, "Sorunlar okunamadı."));
    }
  }, [terminalId]);

  useEffect(() => { void load(); }, [load]);

  const act = async (work: () => Promise<string>) => {
    setBusy(true);
    setActionError(undefined);
    setNotice(undefined);
    try {
      setNotice(await work());
      setResolving(undefined);
      setNote("");
      await load();
    } catch (error) {
      setActionError(failure(error, "İşlem tamamlanamadı."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="online-problems" aria-label="Online sipariş sorunları">
      {loadError && <p className="online-problems__error" role="alert">{loadError}</p>}
      {actionError && <p className="online-problems__error" role="alert">{actionError}</p>}
      <p className="online-problems__notice" role="status" aria-live="polite">{notice ?? ""}</p>
      {problems === null && !loadError && <p>Yükleniyor…</p>}
      {problems?.length === 0 && <p className="online-problems__empty">Açık online sipariş sorunu yok.</p>}
      <ul className="online-problems__list">
        {problems?.map((problem) => (
          <li key={problem.caseId} className="online-problems__item">
            <p className="online-problems__title">
              <strong>{problemKindLabel(problem.kind)}</strong>
              <span>{problem.provider ? platformLabel(problem.provider) : "Platform belirsiz"}</span>
            </p>
            <p className="online-problems__meta">
              {problem.externalOrderId ? `Platform sipariş no: ${problem.externalOrderId} · ` : ""}
              {problem.amount > 0 ? `Tutar: ${formatMoney(problem.amount)} · ` : ""}
              {problemStatusLabel(problem.status)} · {when(problem.openedAt)}
            </p>
            <p className="online-problems__action">Önerilen: {problemActionLabel(problem.nextAction)}</p>
            {canAct && (
              <div className="online-problems__buttons">
                {problem.canRetry && (
                  <button type="button" disabled={busy} onClick={() => void act(async () => { await retryProblem(terminalId, problem.caseId); return "Yeniden denenmek üzere sıraya alındı."; })}>
                    Yeniden dene
                  </button>
                )}
                <button type="button" className="online-problems__secondary" disabled={busy} onClick={() => { setResolving(problem); setNote(""); }}>
                  Çözüldü
                </button>
              </div>
            )}
            {canAct && resolving?.caseId === problem.caseId && (
              <form className="online-problems__resolve" onSubmit={(event) => {
                event.preventDefault();
                void act(async () => { await resolveProblem(terminalId, problem, note.trim()); return "Sorun kapatıldı."; });
              }}>
                <label htmlFor={noteId}>Nasıl çözüldü?</label>
                <textarea id={noteId} value={note} maxLength={500} onChange={(event) => setNote(event.target.value)} />
                <button type="submit" disabled={busy || note.trim() === ""}>Kaydet</button>
                <button type="button" className="online-problems__secondary" onClick={() => setResolving(undefined)}>Vazgeç</button>
              </form>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
