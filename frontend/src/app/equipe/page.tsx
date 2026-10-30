"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { DashboardFrame } from "@/components/DashboardFrame";
import { fieldClass } from "@/components/AuthShell";
import { ApiError, api, getStoredUser, getToken, hasRole } from "@/lib/api";
import { formatDate, label } from "@/lib/format";
import type { Mission, TeamFollowUp, TeamMember, UserProfile } from "@/lib/types";

const statuses = [
  { value: "Backlog", label: "Backlog" },
  { value: "InProgress", label: "En cours" },
  { value: "Done", label: "Terminée" },
];

export default function TeamPage() {
  const router = useRouter();
  const [user, setUser] = useState<UserProfile | null>(null);
  const [members, setMembers] = useState<TeamMember[]>([]);
  const [selected, setSelected] = useState("");
  const [followUp, setFollowUp] = useState<TeamFollowUp | null>(null);
  const [mission, setMission] = useState({ title: "", detail: "", comment: "" });
  const [rating, setRating] = useState({ score: "4", comment: "" });
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }
    const stored = getStoredUser();
    setUser(stored);
    if (!hasRole(stored, "Manager")) return;
    api<TeamMember[]>("/api/team")
      .then((rows) => {
        setMembers(rows);
        if (rows[0]) setSelected(rows[0].id);
      })
      .catch((caught) => setError(caught instanceof ApiError ? caught.message : "Chargement impossible."));
  }, [router]);

  useEffect(() => {
    if (!selected) return;
    api<TeamFollowUp>(`/api/team/${selected}`)
      .then(setFollowUp)
      .catch((caught) => setError(caught instanceof ApiError ? caught.message : "Chargement impossible."));
  }, [selected]);

  if (!user) return null;

  async function reload() {
    setFollowUp(await api<TeamFollowUp>(`/api/team/${selected}`));
  }

  async function addMission() {
    setError(null);
    try {
      await api(`/api/team/${selected}/missions`, { method: "POST", body: JSON.stringify(mission) });
      setMission({ title: "", detail: "", comment: "" });
      await reload();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Mission impossible.");
    }
  }

  async function saveMission(item: Mission, status: string, comment: string) {
    setError(null);
    try {
      await api(`/api/team/missions/${item.id}`, { method: "PUT", body: JSON.stringify({ status, comment }) });
      await reload();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Mise à jour impossible.");
    }
  }

  async function saveRating() {
    setError(null);
    const today = new Date().toISOString().slice(0, 10);
    try {
      await api(`/api/team/${selected}/rating`, {
        method: "PUT",
        body: JSON.stringify({ weekStart: today, score: Number(rating.score), comment: rating.comment }),
      });
      setRating({ score: "4", comment: "" });
      await reload();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Note impossible.");
    }
  }

  return (
    <DashboardFrame user={user}>
      <header className="auth-rise">
        <p className="text-sm text-neutral-500">Manager</p>
        <h1 className="mt-2 text-4xl font-medium tracking-tight">Équipe</h1>
      </header>
      {!hasRole(user, "Manager") ? <p className="mt-8 text-sm text-neutral-500">Cette page est réservée au manager.</p> : (
        <>
          {error ? <p className="mt-4 text-sm">{error}</p> : null}
          <label className="mt-8 block text-sm">Collaborateur
            <select className={fieldClass} value={selected} onChange={(event) => setSelected(event.target.value)}>
              {members.map((member) => (
                <option key={member.id} value={member.id}>{member.fullName} · {member.positionTitle}</option>
              ))}
            </select>
          </label>
          {followUp ? (
            <div className="mt-6 grid gap-4 lg:grid-cols-2">
              <section className="rounded-2xl border border-neutral-200 p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)]">
                <h2 className="text-lg font-medium">Nouvelle mission</h2>
                <label className="mt-3 block text-sm">Titre<input className={fieldClass} value={mission.title} onChange={(event) => setMission({ ...mission, title: event.target.value })} /></label>
                <label className="mt-3 block text-sm">Détail<input className={fieldClass} value={mission.detail} onChange={(event) => setMission({ ...mission, detail: event.target.value })} /></label>
                <label className="mt-3 block text-sm">Commentaire<input className={fieldClass} value={mission.comment} onChange={(event) => setMission({ ...mission, comment: event.target.value })} /></label>
                <button type="button" className="mt-4 rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={addMission}>Ajouter au backlog</button>
              </section>
              <section className="rounded-2xl border border-neutral-200 p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)]">
                <h2 className="text-lg font-medium">Score de la semaine</h2>
                <label className="mt-3 block text-sm">Note sur 5<input type="number" min="1" max="5" className={fieldClass} value={rating.score} onChange={(event) => setRating({ ...rating, score: event.target.value })} /></label>
                <label className="mt-3 block text-sm">Commentaire<input className={fieldClass} value={rating.comment} onChange={(event) => setRating({ ...rating, comment: event.target.value })} /></label>
                <button type="button" className="mt-4 rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={saveRating}>Enregistrer</button>
                <ul className="mt-4 flex flex-col gap-2">
                  {followUp.ratings.map((item) => (
                    <li key={item.id} className="text-sm">{item.score}/5 · semaine du {formatDate(item.weekStart)} · {item.comment}</li>
                  ))}
                </ul>
              </section>
              <section className="rounded-2xl border border-neutral-200 p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)] lg:col-span-2">
                <h2 className="text-lg font-medium">Backlog</h2>
                <ul className="mt-4 flex flex-col gap-4">
                  {followUp.missions.map((item) => (
                    <MissionRow key={item.id} mission={item} onSave={saveMission} />
                  ))}
                </ul>
              </section>
            </div>
          ) : null}
        </>
      )}
    </DashboardFrame>
  );
}

function MissionRow({ mission, onSave }: { mission: Mission; onSave: (mission: Mission, status: string, comment: string) => void }) {
  const [status, setStatus] = useState(mission.status);
  const [comment, setComment] = useState(mission.comment ?? "");
  return (
    <li className="border-t border-neutral-200 pt-4 first:border-0 first:pt-0">
      <p className="font-medium">{mission.title}</p>
      <p className="text-sm text-neutral-500">{label(mission.status)} · semaine du {formatDate(mission.weekStart)}</p>
      {mission.detail ? <p className="mt-1 text-sm">{mission.detail}</p> : null}
      <div className="mt-3 grid gap-3 md:grid-cols-[12rem_1fr_auto]">
        <select className={fieldClass} value={status} onChange={(event) => setStatus(event.target.value)}>
          {statuses.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
        </select>
        <input className={fieldClass} value={comment} onChange={(event) => setComment(event.target.value)} />
        <button type="button" className="rounded-xl border border-black px-4 py-2.5 text-sm" onClick={() => onSave(mission, status, comment)}>Enregistrer</button>
      </div>
    </li>
  );
}
