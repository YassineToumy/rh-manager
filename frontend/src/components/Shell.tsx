"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { api, clearSession, getStoredUser, getToken, hasRole, staffRoles } from "@/lib/api";
import type { NotificationItem, UserProfile } from "@/lib/types";
import { label } from "@/lib/format";

const links: { href: string; label: string; roles?: string[] }[] = [
  { href: "/tableau-de-bord", label: "Tableau de bord" },
  { href: "/employes", label: "Employés" },
  { href: "/organisation", label: "Organisation" },
  { href: "/contrats", label: "Contrats" },
  { href: "/documents", label: "Documents" },
  { href: "/conges", label: "Congés" },
  { href: "/presences", label: "Présences" },
  { href: "/recrutement", label: "Recrutement", roles: staffRoles },
  { href: "/evaluations", label: "Évaluations" },
  { href: "/parametres", label: "Accès", roles: ["Administrateur"] },
];

export function Shell({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [user, setUser] = useState<UserProfile | null>(null);
  const [notes, setNotes] = useState<NotificationItem[]>([]);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }
    setUser(getStoredUser());
    api<UserProfile>("/api/auth/me")
      .then(setUser)
      .catch(() => router.replace("/login"));
    api<NotificationItem[]>("/api/notifications").then(setNotes).catch(() => setNotes([]));
  }, [router]);

  async function markRead(id: string) {
    await api(`/api/notifications/${id}/read`, { method: "POST" });
    setNotes((current) => current.map((note) => (note.id === id ? { ...note, readAt: new Date().toISOString() } : note)));
  }

  function logout() {
    clearSession();
    router.replace("/login");
  }

  const unread = notes.filter((note) => !note.readAt).length;
  const visible = links.filter((item) => !item.roles || hasRole(user, ...item.roles));

  return (
    <div className="min-h-screen md:grid md:grid-cols-[240px_1fr]">
      <aside className="bg-[#1c2430] px-4 py-6 text-[#f3efe6]">
        <p className="px-2 font-[family-name:var(--font-display)] text-2xl">HRFlow</p>
        <p className="mt-1 px-2 text-xs text-[#c8bfb0]">{user ? `${user.firstName} · ${user.roles.map(label).join(", ")}` : "…"}</p>
        <nav className="mt-8 flex flex-col gap-1">
          {visible.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={`rounded-xl px-3 py-2 text-sm ${pathname.startsWith(item.href) ? "bg-[#0f6e6e] text-white" : "hover:bg-white/10"}`}
            >
              {item.label}
            </Link>
          ))}
        </nav>
      </aside>
      <div>
        <div className="flex items-center justify-end gap-3 border-b border-[#e4dccb] px-6 py-3">
          <div className="relative">
            <button className="rounded-full border border-[#d9d0c1] bg-white px-3 py-1 text-sm" onClick={() => setOpen((value) => !value)}>
              Notifications {unread > 0 ? `(${unread})` : ""}
            </button>
            {open ? (
              <div className="absolute right-0 z-10 mt-2 w-80 rounded-2xl border border-[#e4dccb] bg-white p-3 shadow-lg">
                {notes.length === 0 ? <p className="text-sm text-[#5c6570]">Aucune notification.</p> : null}
                {notes.map((note) => (
                  <button key={note.id} className="block w-full border-b border-[#f3efe6] py-2 text-left text-sm" onClick={() => markRead(note.id)}>
                    <span className={note.readAt ? "text-[#5c6570]" : "font-medium"}>{note.title}</span>
                    <span className="mt-1 block text-xs text-[#5c6570]">{note.body}</span>
                  </button>
                ))}
              </div>
            ) : null}
          </div>
          <button className="text-sm underline" onClick={logout}>
            Déconnexion
          </button>
        </div>
        <main className="px-6 py-6">{children}</main>
      </div>
    </div>
  );
}
