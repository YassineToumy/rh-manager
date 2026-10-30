"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { ChoiceList } from "@/components/ChoiceList";
import { DashboardFrame } from "@/components/DashboardFrame";
import { fieldClass } from "@/components/AuthShell";
import { ApiError, api, getStoredUser, getToken, hasRole } from "@/lib/api";
import { formatDate, label } from "@/lib/format";
import type { Account, UserProfile } from "@/lib/types";

const roles = [
  { value: "Employe", label: "Employé" },
  { value: "ResponsableRH", label: "Responsable RH" },
  { value: "Manager", label: "Manager" },
];

type Filters = { role: string; status: string; createdFrom: string; createdTo: string };
type Draft = { firstName: string; lastName: string; email: string; password: string; role: string };

const emptyFilters: Filters = { role: "", status: "", createdFrom: "", createdTo: "" };
const emptyDraft: Draft = { firstName: "", lastName: "", email: "", password: "", role: "Employe" };

export default function EmployeesPage() {
  const router = useRouter();
  const [user, setUser] = useState<UserProfile | null>(null);
  const [filters, setFilters] = useState<Filters>(emptyFilters);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [draft, setDraft] = useState<Draft>(emptyDraft);
  const [editing, setEditing] = useState<Account | null>(null);
  const [removing, setRemoving] = useState<Account | null>(null);

  const load = useCallback(async (next: Filters) => {
    const params = new URLSearchParams();
    if (next.role) params.set("role", next.role);
    if (next.status) params.set("status", next.status);
    if (next.createdFrom) params.set("createdFrom", next.createdFrom);
    if (next.createdTo) params.set("createdTo", next.createdTo);
    const query = params.toString();
    const rows = await api<Account[]>(`/api/users${query ? `?${query}` : ""}`);
    setAccounts(rows);
  }, []);

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }
    const stored = getStoredUser();
    setUser(stored);
    if (hasRole(stored, "Administrateur")) {
      load(emptyFilters).catch((caught) => setError(caught instanceof ApiError ? caught.message : "Chargement impossible."));
    }
  }, [load, router]);

  if (!user) return null;

  async function refresh() {
    setError(null);
    try {
      await load(filters);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Chargement impossible.");
    }
  }

  async function createAccount() {
    setError(null);
    try {
      await api("/api/users", { method: "POST", body: JSON.stringify(draft) });
      setDraft(emptyDraft);
      setCreating(false);
      await load(filters);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Création impossible.");
    }
  }

  async function saveAccount() {
    if (!editing) return;
    setError(null);
    try {
      await api(`/api/users/${editing.id}`, {
        method: "PUT",
        body: JSON.stringify({
          firstName: editing.firstName,
          lastName: editing.lastName,
          email: editing.email,
          role: editing.role,
        }),
      });
      setEditing(null);
      await load(filters);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Modification impossible.");
    }
  }

  async function approve(account: Account) {
    setError(null);
    try {
      await api(`/api/users/${account.id}/approval`, { method: "PUT", body: JSON.stringify({ isApproved: true }) });
      await load(filters);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Acceptation impossible.");
    }
  }

  async function remove() {
    if (!removing) return;
    setError(null);
    try {
      await api(`/api/users/${removing.id}`, { method: "DELETE" });
      setRemoving(null);
      await load(filters);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Suppression impossible.");
    }
  }

  return (
    <DashboardFrame user={user}>
      <header className="auth-rise flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm text-neutral-500">Administration</p>
          <h1 className="mt-2 text-4xl font-medium tracking-tight">Comptes</h1>
        </div>
        {hasRole(user, "Administrateur") ? (
          <button type="button" className="rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={() => setCreating((open) => !open)}>
            Ajouter un compte
          </button>
        ) : null}
      </header>

      {!hasRole(user, "Administrateur") ? (
        <p className="mt-8 text-sm text-neutral-500">Cette liste est réservée à l'administrateur.</p>
      ) : (
        <>
          <form
            className="auth-rise mt-8 grid gap-3 rounded-2xl border border-neutral-200 p-4 shadow-[0_8px_24px_rgba(0,0,0,0.06)] md:grid-cols-4"
            onSubmit={(event) => {
              event.preventDefault();
              refresh();
            }}
          >
            <label className="text-sm">
              Type
              <select className={fieldClass} value={filters.role} onChange={(event) => setFilters({ ...filters, role: event.target.value })}>
                <option value="">Tous</option>
                {roles.map((role) => (
                  <option key={role.value} value={role.value}>{role.label}</option>
                ))}
              </select>
            </label>
            <label className="text-sm">
              Statut
              <select className={fieldClass} value={filters.status} onChange={(event) => setFilters({ ...filters, status: event.target.value })}>
                <option value="">Tous</option>
                <option value="pending">En attente</option>
                <option value="approved">Accepté</option>
              </select>
            </label>
            <label className="text-sm">
              Créé depuis
              <input type="date" className={fieldClass} value={filters.createdFrom} onChange={(event) => setFilters({ ...filters, createdFrom: event.target.value })} />
            </label>
            <label className="text-sm">
              Créé jusqu'au
              <input type="date" className={fieldClass} value={filters.createdTo} onChange={(event) => setFilters({ ...filters, createdTo: event.target.value })} />
            </label>
            <button type="submit" className="rounded-xl border border-black px-4 py-2.5 text-sm md:col-span-4 md:justify-self-start">
              Filtrer
            </button>
          </form>

          {creating ? (
            <div className="mt-4 rounded-2xl border border-neutral-200 p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)]">
              <div className="grid gap-3 md:grid-cols-2">
                <label className="text-sm">Prénom<input className={fieldClass} value={draft.firstName} onChange={(event) => setDraft({ ...draft, firstName: event.target.value })} /></label>
                <label className="text-sm">Nom<input className={fieldClass} value={draft.lastName} onChange={(event) => setDraft({ ...draft, lastName: event.target.value })} /></label>
                <label className="text-sm">E-mail<input type="email" className={fieldClass} value={draft.email} onChange={(event) => setDraft({ ...draft, email: event.target.value })} /></label>
                <label className="text-sm">Mot de passe<input type="password" className={fieldClass} value={draft.password} onChange={(event) => setDraft({ ...draft, password: event.target.value })} /></label>
              </div>
              <ChoiceList label="Rôle" value={draft.role} options={roles} onChange={(role) => setDraft({ ...draft, role: role ?? "" })} />
              <button type="button" className="mt-4 rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={createAccount}>
                Créer
              </button>
            </div>
          ) : null}

          {error ? <p className="mt-4 text-sm">{error}</p> : null}

          <ul className="mt-6 flex flex-col gap-3">
            {accounts.map((account) => (
              <li key={account.id} className="rounded-2xl border border-neutral-200 p-4 shadow-[0_8px_24px_rgba(0,0,0,0.06)]">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="font-medium">{account.firstName} {account.lastName}</p>
                    <p className="text-sm text-neutral-500">{account.email}</p>
                    <p className="mt-2 text-sm">{label(account.role)} · {account.isApproved ? "Accepté" : "En attente"} · {formatDate(account.createdAt)}</p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {!account.isApproved ? (
                      <button type="button" className="rounded-xl bg-black px-3 py-2 text-sm text-white" onClick={() => approve(account)}>Accepter</button>
                    ) : null}
                    <button type="button" className="rounded-xl border border-black px-3 py-2 text-sm" onClick={() => setEditing(account)}>Modifier</button>
                    <button type="button" className="rounded-xl border border-black px-3 py-2 text-sm" onClick={() => setRemoving(account)}>Supprimer</button>
                  </div>
                </div>
              </li>
            ))}
            {accounts.length === 0 ? <li className="text-sm text-neutral-500">Aucun compte pour ce filtre.</li> : null}
          </ul>
        </>
      )}

      {editing ? (
        <div className="auth-backdrop fixed inset-0 z-20 flex items-center justify-center bg-black/50 px-4" role="dialog" aria-modal="true">
          <div className="auth-dialog w-full max-w-md rounded-2xl bg-white p-6 text-black">
            <h2 className="text-xl font-medium">Modifier le compte</h2>
            <label className="mt-4 block text-sm">Prénom<input className={fieldClass} value={editing.firstName} onChange={(event) => setEditing({ ...editing, firstName: event.target.value })} /></label>
            <label className="mt-3 block text-sm">Nom<input className={fieldClass} value={editing.lastName} onChange={(event) => setEditing({ ...editing, lastName: event.target.value })} /></label>
            <label className="mt-3 block text-sm">E-mail<input type="email" className={fieldClass} value={editing.email} onChange={(event) => setEditing({ ...editing, email: event.target.value })} /></label>
            <ChoiceList label="Rôle" value={editing.role} options={roles} onChange={(role) => setEditing({ ...editing, role: role ?? editing.role })} />
            <div className="mt-5 flex gap-2">
              <button type="button" className="rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={saveAccount}>Enregistrer</button>
              <button type="button" className="rounded-xl border border-black px-4 py-2.5 text-sm" onClick={() => setEditing(null)}>Annuler</button>
            </div>
          </div>
        </div>
      ) : null}

      {removing ? (
        <div className="auth-backdrop fixed inset-0 z-20 flex items-center justify-center bg-black/50 px-4" role="dialog" aria-modal="true">
          <div className="auth-dialog w-full max-w-sm rounded-2xl bg-white p-6 text-black">
            <p>Supprimer le compte de {removing.firstName} {removing.lastName} ?</p>
            <div className="mt-5 flex gap-2">
              <button type="button" className="rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={remove}>Supprimer</button>
              <button type="button" className="rounded-xl border border-black px-4 py-2.5 text-sm" onClick={() => setRemoving(null)}>Annuler</button>
            </div>
          </div>
        </div>
      ) : null}
    </DashboardFrame>
  );
}
