"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { DashboardFrame } from "@/components/DashboardFrame";
import { EmployeeSpace } from "@/components/EmployeeSpace";
import { getStoredUser, getToken, hasRole } from "@/lib/api";
import type { UserProfile } from "@/lib/types";

const overview = [
  { title: "Employés", text: "Effectif suivi" },
  { title: "Contrats", text: "Contrats en cours" },
  { title: "Congés", text: "Demandes à traiter" },
  { title: "Présences", text: "Journée en cours" },
];

const personal = [
  { title: "Heures travaillées", text: "Total sur la période" },
  { title: "Solde de congés", text: "Jours restants" },
];

export default function Home() {
  const router = useRouter();
  const [user, setUser] = useState<UserProfile | null>(null);

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }
    setUser(getStoredUser());
  }, [router]);

  if (!user) return null;

  if (hasRole(user, "Employe") && !hasRole(user, "Administrateur", "ResponsableRH", "Manager")) {
    return (
      <DashboardFrame user={user}>
        <EmployeeSpace section="home" firstName={user.firstName} />
      </DashboardFrame>
    );
  }

  return (
    <DashboardFrame user={user}>
      <header className="auth-rise">
        <p className="text-sm text-neutral-500">Tableau de bord</p>
        <h1 className="mt-2 text-4xl font-medium tracking-tight">Bonjour, {user.firstName}</h1>
      </header>
      <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {overview.map((card, index) => (
          <article
            key={card.title}
            className="auth-rise rounded-2xl border border-neutral-200 bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)]"
            style={{ animationDelay: `${80 + index * 60}ms` }}
          >
            <p className="text-sm text-neutral-500">{card.text}</p>
            <p className="mt-3 text-3xl font-medium tracking-tight">—</p>
            <p className="mt-2 text-sm font-medium">{card.title}</p>
          </article>
        ))}
      </div>
      <h2 className="auth-rise mt-10 text-lg font-medium tracking-tight" style={{ animationDelay: "320ms" }}>
        Votre espace
      </h2>
      <div className="mt-4 grid gap-4 md:grid-cols-2">
        {personal.map((card, index) => (
          <article
            key={card.title}
            className="auth-rise rounded-2xl border border-neutral-200 bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)]"
            style={{ animationDelay: `${380 + index * 60}ms` }}
          >
            <p className="text-sm text-neutral-500">{card.text}</p>
            <p className="mt-3 text-3xl font-medium tracking-tight">—</p>
            <p className="mt-2 text-sm font-medium">{card.title}</p>
          </article>
        ))}
      </div>
    </DashboardFrame>
  );
}
