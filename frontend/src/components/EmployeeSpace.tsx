"use client";

import Link from "next/link";
import { useEffect, useState, type ReactNode } from "react";
import { fieldClass } from "@/components/AuthShell";
import { ApiError, api } from "@/lib/api";
import { formatDate, label } from "@/lib/format";
import type { EmployeeSpace as Space, LeaveBalance } from "@/lib/types";

const money = new Intl.NumberFormat("fr-FR", { style: "currency", currency: "EUR" });

function moins(days: number) {
  const text = days.toLocaleString("fr-FR");
  return `Moins ${text} jour${days > 1 ? "s" : ""}`;
}

export function EmployeeSpace({ section, firstName }: { section: "home" | "leave" | "salary"; firstName: string }) {
  const [space, setSpace] = useState<Space | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [leave, setLeave] = useState({ leaveTypeId: "", startDate: "", endDate: "", reason: "", halfDay: false, halfDayPart: "Morning" });
  const [advance, setAdvance] = useState({ amount: "", reason: "" });
  const [loading, setLoading] = useState(true);

  async function load() {
    const next = await api<Space>("/api/me/space");
    setSpace(next);
    setLeave((current) => ({ ...current, leaveTypeId: current.leaveTypeId || next.leaveBalances?.[0]?.leaveTypeId || "" }));
  }

  useEffect(() => {
    load()
      .catch((caught) => setError(caught instanceof ApiError ? caught.message : "Chargement impossible."))
      .finally(() => setLoading(false));
  }, []);

  async function submitLeave() {
    setError(null);
    setNotice(null);
    try {
      await api("/api/me/space/leave", {
        method: "POST",
        body: JSON.stringify({
          leaveTypeId: leave.leaveTypeId,
          startDate: leave.startDate,
          endDate: leave.halfDay ? leave.startDate : leave.endDate,
          reason: leave.reason,
          halfDay: leave.halfDay,
          halfDayPart: leave.halfDay ? leave.halfDayPart : null,
        }),
      });
      setLeave((current) => ({ ...current, startDate: "", endDate: "", reason: "", halfDay: false, halfDayPart: "Morning" }));
      setNotice("Demande de congé envoyée.");
      await load();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Demande impossible.");
    }
  }

  async function submitAdvance() {
    setError(null);
    setNotice(null);
    try {
      await api("/api/me/space/advances", {
        method: "POST",
        body: JSON.stringify({ amount: Number(advance.amount), reason: advance.reason }),
      });
      setAdvance({ amount: "", reason: "" });
      setNotice("Demande d'avance envoyée.");
      await load();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Demande impossible.");
    }
  }

  const period = space
    ? new Date(space.salaryYear, space.salaryMonth - 1, 1).toLocaleDateString("fr-FR", { month: "long", year: "numeric" })
    : "";
  const titles = { home: "Bonjour", leave: "Congés", salary: "Salaire" };
  const cut = space?.salaryCut;
  const net = space ? space.baseSalary + space.bonus - (cut?.amount ?? 0) : 0;

  return (
    <>
      <header className="auth-rise">
        <p className="text-sm text-neutral-500">{section === "home" ? "Votre espace" : "Espace employé"}</p>
        <h1 className="mt-2 text-4xl font-medium tracking-tight">
          {section === "home" ? `${titles.home}, ${firstName}` : titles[section]}
        </h1>
      </header>
      {error ? <p className="mt-6 rounded-2xl border border-neutral-200 bg-white p-5 text-sm shadow-[0_8px_24px_rgba(0,0,0,0.06)]">{error}</p> : null}
      {notice ? <p className="mt-4 text-sm">{notice}</p> : null}
      {loading && !space ? <p className="mt-8 text-sm text-neutral-500">Chargement de votre espace…</p> : null}
      {!space ? null : section === "home" ? (
        <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          <Kpi href="/salaire" label="Heures cette semaine" value={`${(space.weeks?.[0]?.hours ?? 0).toLocaleString("fr-FR")} h`} text="Pointage complet" />
          <Kpi href="/salaire" label="Heures ce mois" value={`${space.hoursThisMonth.toLocaleString("fr-FR")} h`} text={period} />
          <Kpi
            href="/salaire"
            label="Salaire ce mois"
            value={money.format(net)}
            text={cut && cut.unpaidDays > 0 ? moins(cut.unpaidDays) : space.salaryPaid ? "Versé" : "Pas encore versé"}
          />
          <Kpi href="/conges" label="Solde de congés" value={`${space.leave.remainingDays.toLocaleString("fr-FR")} j`} text="1,5 jour par mois révolu" />
          <Kpi label="Note de la semaine" value={space.ratings[0] ? `${space.ratings[0].score}/5` : "—"} text={space.ratings[0]?.managerName ?? "Aucune note"} />
          <Kpi label="Missions" value={String((space.missions ?? []).length)} text="Données par le manager" />
        </div>
      ) : null}
      {space && section === "home" && (space.missions ?? []).length > 0 ? (
        <article className="auth-rise mt-4 rounded-2xl border border-neutral-200 bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)] dark:border-neutral-800 dark:bg-neutral-900">
          <p className="text-sm text-neutral-500">Écrites par le manager</p>
          <h2 className="mt-1 text-lg font-medium">Missions</h2>
          <ul className="mt-4 flex flex-col gap-3">
            {space.missions.map((mission) => (
              <li key={mission.id}>
                <p className="font-medium">{mission.title}</p>
                <p className="text-sm text-neutral-500">{label(mission.status)} · {mission.managerName}</p>
                {mission.comment ? <p className="mt-1 text-sm">Commentaire : {mission.comment}</p> : null}
              </li>
            ))}
          </ul>
        </article>
      ) : null}
      {space && section === "leave" ? (
        <div className="mt-8 grid gap-4 lg:grid-cols-2">
          <Card title="Solde de congés" text="1,5 jour par mois depuis la création du compte">
            <p className="text-3xl font-medium tracking-tight">{space.leave.remainingDays.toLocaleString("fr-FR")} j</p>
            <p className="mt-3 text-sm text-neutral-500">Depuis le {formatDate(space.leave.accountCreatedOn)} · {space.leave.months} mois</p>
            <p className="mt-1 text-sm">Acquis {space.leave.accruedDays.toLocaleString("fr-FR")} j · réservé {space.leave.reservedDays.toLocaleString("fr-FR")} j</p>
          </Card>
          <Card title="Comment le solde fonctionne" text="Congés et retenue sur salaire">
            <p className="text-sm leading-6">
              Le solde augmente de 1,5 jour à chaque mois révolu depuis la création du compte. Une demande est d'abord prise sur ce solde.
              Si le solde ne couvre pas tous les jours, les jours manquants sont retirés du salaire du mois. Une demi-journée compte pour 0,5 jour.
            </p>
          </Card>
          <Card title="Demande de congé" text="Motif, dates, et demi-journée si besoin">
            <label className="text-sm">Type
              <select className={fieldClass} value={leave.leaveTypeId} onChange={(event) => setLeave({ ...leave, leaveTypeId: event.target.value })}>
                {(space.leaveBalances ?? []).map((balance: LeaveBalance) => (
                  <option key={balance.leaveTypeId} value={balance.leaveTypeId}>{balance.leaveTypeName}</option>
                ))}
              </select>
            </label>
            <label className="mt-3 block text-sm">Pourquoi ce congé
              <input className={fieldClass} value={leave.reason} onChange={(event) => setLeave({ ...leave, reason: event.target.value })} />
            </label>
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <label className="text-sm">Date de début
                <input type="date" className={fieldClass} value={leave.startDate} onChange={(event) => setLeave({ ...leave, startDate: event.target.value })} />
              </label>
              {leave.halfDay ? null : (
                <label className="text-sm">Date de fin
                  <input type="date" className={fieldClass} value={leave.endDate} onChange={(event) => setLeave({ ...leave, endDate: event.target.value })} />
                </label>
              )}
            </div>
            <label className="mt-3 flex items-center gap-2 text-sm">
              <input type="checkbox" checked={leave.halfDay} onChange={(event) => setLeave({ ...leave, halfDay: event.target.checked })} />
              Demi-journée
            </label>
            {leave.halfDay ? (
              <label className="mt-3 block text-sm">Quelle demi-journée
                <select className={fieldClass} value={leave.halfDayPart} onChange={(event) => setLeave({ ...leave, halfDayPart: event.target.value })}>
                  <option value="Morning">Matin</option>
                  <option value="Afternoon">Après-midi</option>
                </select>
              </label>
            ) : null}
            <button type="button" className="mt-4 rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={submitLeave}>Envoyer</button>
            <ul className="mt-4 flex flex-col gap-2">
              {(space.leaveRequests ?? []).map((request) => (
                <li key={request.id} className="text-sm text-neutral-600">
                  {request.leaveTypeName} · {formatDate(request.startDate)}
                  {request.isHalfDay ? ` · demi-journée ${label(request.halfDayPart ?? "")}` : ` – ${formatDate(request.endDate)}`}
                  {" · "}{label(request.status)}
                  {request.reason ? ` · ${request.reason}` : ""}
                  {(request.unpaidDays ?? 0) > 0 ? ` · ${moins(request.unpaidDays ?? 0)} sur le salaire` : ""}
                </li>
              ))}
            </ul>
          </Card>
        </div>
      ) : null}
      {space && section === "salary" ? (
        <div className="mt-8 grid gap-4 lg:grid-cols-2">
          <Card title="Salaire" text={period}>
            <p className="text-3xl font-medium tracking-tight">{space.salaryPaid ? "Versé" : "Pas encore versé"}</p>
            <p className="mt-4 text-sm text-neutral-500">Salaire actuel</p>
            <p className="text-lg">{money.format(space.baseSalary)}</p>
            <p className="mt-3 text-sm text-neutral-500">Primes</p>
            <p className="text-lg">{money.format(space.bonus)}</p>
            <p className="mt-3 text-sm text-neutral-500">Net du mois</p>
            <p className="text-lg">{money.format(net)}</p>
            <p className="mt-4 text-sm text-neutral-500">Puce de connexion</p>
            {space.chipNumber ? (
              <>
                <p className="text-lg">{space.chipNumber}</p>
                <p className="text-sm text-neutral-500">Crédit rechargeable {money.format(space.chipCredit ?? 0)}</p>
              </>
            ) : (
              <p className="text-sm">Aucune puce attribuée.</p>
            )}
          </Card>
          <Card title="Retenue sur salaire" text="Jours de congé non couverts par le solde">
            {cut && cut.unpaidDays > 0 ? (
              <>
                <p className="text-3xl font-medium tracking-tight">{moins(cut.unpaidDays)}</p>
                <p className="mt-3 text-sm">Retenue {money.format(cut.amount)}</p>
                <p className="mt-2 text-sm text-neutral-500">
                  Le solde ne couvrait pas {cut.unpaidDays.toLocaleString("fr-FR")} jour{cut.unpaidDays > 1 ? "s" : ""}.
                  Chaque jour ouvré du mois vaut {money.format(cut.dailyRate)}.
                </p>
              </>
            ) : (
              <p className="text-sm text-neutral-500">Aucune retenue ce mois.</p>
            )}
          </Card>
          <Card title="Demande d'avance" text="Traitée ensuite par les ressources humaines">
            <label className="text-sm">Montant<input type="number" min="1" className={fieldClass} value={advance.amount} onChange={(event) => setAdvance({ ...advance, amount: event.target.value })} /></label>
            <label className="mt-3 block text-sm">Motif<input className={fieldClass} value={advance.reason} onChange={(event) => setAdvance({ ...advance, reason: event.target.value })} /></label>
            <button type="button" className="mt-4 rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={submitAdvance}>Envoyer</button>
            <ul className="mt-4 flex flex-col gap-2">
              {space.advances.map((item) => (
                <li key={item.id} className="text-sm text-neutral-600">{money.format(item.amount)} · {label(item.status)} · {formatDate(item.createdAt)}</li>
              ))}
            </ul>
          </Card>
          <Card title="Données professionnelles" text="Le mot de passe se change dans Paramètres">
            <dl className="grid gap-3 text-sm">
              <div><dt className="text-neutral-500">N° de poste</dt><dd>{space.positionNumber}</dd></div>
              <div><dt className="text-neutral-500">E-mail professionnel</dt><dd>{space.professionalEmail}</dd></div>
              <div><dt className="text-neutral-500">RIB</dt><dd>{space.bankRib || "—"}</dd></div>
            </dl>
          </Card>
        </div>
      ) : null}
    </>
  );
}

function Kpi({ href, label: title, value, text }: { href?: string; label: string; value: string; text: string }) {
  const body = (
    <>
      <p className="text-sm text-neutral-500">{text}</p>
      <p className="mt-3 text-3xl font-medium tracking-tight">{value}</p>
      <p className="mt-2 text-sm font-medium">{title}</p>
    </>
  );
  const className = "auth-rise rounded-2xl border border-neutral-200 bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)] dark:border-neutral-800 dark:bg-neutral-900";
  return href ? <Link href={href} className={className}>{body}</Link> : <article className={className}>{body}</article>;
}

function Card({ title, text, children }: { title: string; text: string; children: ReactNode }) {
  return (
    <article className="auth-rise rounded-2xl border border-neutral-200 bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)] dark:border-neutral-800 dark:bg-neutral-900">
      <p className="text-sm text-neutral-500">{text}</p>
      <h2 className="mt-1 text-lg font-medium">{title}</h2>
      <div className="mt-4">{children}</div>
    </article>
  );
}
