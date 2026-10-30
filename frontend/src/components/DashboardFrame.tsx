"use client";

import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { clearSession, fetchBlob, getToken } from "@/lib/api";
import { label } from "@/lib/format";
import type { UserProfile } from "@/lib/types";

const services = [
  { label: "Tableau de bord", icon: "dashboard", href: "/", roles: ["Administrateur", "ResponsableRH", "Manager", "Employe"] },
  { label: "Congés", icon: "calendar", href: "/conges", roles: ["Employe"] },
  { label: "Salaire", icon: "wallet", href: "/salaire", roles: ["Employe"] },
  { label: "Employés", icon: "people", href: "/employes", roles: ["Administrateur"] },
  { label: "Contrats", icon: "contract", href: "", roles: ["Administrateur", "ResponsableRH", "Manager"] },
  { label: "Documents", icon: "folder", href: "", roles: ["Administrateur", "ResponsableRH", "Manager"] },
  { label: "Congés", icon: "calendar", href: "", roles: ["Administrateur", "ResponsableRH", "Manager"] },
  { label: "Présences", icon: "clock", href: "", roles: ["Administrateur", "ResponsableRH", "Manager"] },
  { label: "Recrutement", icon: "search", href: "", roles: ["Administrateur", "ResponsableRH"] },
  { label: "Équipe", icon: "people", href: "/equipe", roles: ["Manager"] },
  { label: "Évaluations", icon: "chart", href: "", roles: ["Administrateur", "ResponsableRH"] },
  { label: "Paramètres", icon: "settings", href: "/parametres", roles: ["Administrateur", "ResponsableRH", "Manager", "Employe"] },
] as const;

export function DashboardFrame({ user, children }: { user: UserProfile; children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [collapsed, setCollapsed] = useState(false);
  const [photo, setPhoto] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    let url: string | null = null;
    const load = () => {
      if (!getToken()) return;
      fetchBlob("/api/auth/photo")
        .then((blob) => {
          if (cancelled) return;
          if (url) URL.revokeObjectURL(url);
          if (!blob) {
            url = null;
            setPhoto(null);
            return;
          }
          url = URL.createObjectURL(blob);
          setPhoto(url);
        })
        .catch(() => setPhoto(null));
    };
    load();
    window.addEventListener("hrflow-photo", load);
    return () => {
      cancelled = true;
      window.removeEventListener("hrflow-photo", load);
      if (url) URL.revokeObjectURL(url);
    };
  }, [user.id]);

  return (
    <div
      className="grid h-screen overflow-hidden bg-white text-black transition-[grid-template-columns] duration-300 ease-out dark:bg-neutral-950 dark:text-neutral-100"
      style={{ gridTemplateColumns: collapsed ? "5rem minmax(0,1fr)" : "17.5rem minmax(0,1fr)" }}
    >
      <aside className="relative flex flex-col bg-black text-white">
        <div className={`flex items-center px-4 pt-8 ${collapsed ? "justify-center px-3" : "justify-between"}`}>
          <p className={`text-sm font-medium tracking-[0.22em] whitespace-nowrap ${collapsed ? "hidden" : ""}`}>HRFLOW</p>
          <button
            type="button"
            aria-expanded={!collapsed}
            aria-label={collapsed ? "Ouvrir le menu" : "Fermer le menu"}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-white text-black transition duration-300 hover:scale-105"
            onClick={() => setCollapsed((open) => !open)}
          >
            <svg viewBox="0 0 16 16" className={`h-4 w-4 transition duration-300 ${collapsed ? "rotate-180" : ""}`} aria-hidden>
              <path d="M10 3.5 5.5 8 10 12.5" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>
        </div>
        <div className={`overflow-hidden px-4 transition-all duration-300 ${collapsed ? "px-3" : ""}`}>
          <div className={`overflow-hidden transition-all duration-300 ${collapsed ? "mt-0 max-h-0 opacity-0" : "mt-6 max-h-28 opacity-100"}`}>
            <div className="flex items-center gap-3">
              <span className="grid h-9 w-9 shrink-0 overflow-hidden rounded-full bg-white/15 text-xs font-medium">
                {photo ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={photo} alt="" className="h-full w-full object-cover" />
                ) : (
                  <span className="grid h-full w-full place-items-center">{user.firstName.slice(0, 1)}{user.lastName.slice(0, 1)}</span>
                )}
              </span>
              <p className="text-lg font-medium tracking-tight whitespace-nowrap">
                {user.firstName} {user.lastName}
              </p>
            </div>
            <p className="mt-1 text-sm whitespace-nowrap text-white/60">{user.roles.map(label).join(", ")}</p>
          </div>
        </div>
        <nav className="mt-8 flex flex-1 flex-col gap-1 overflow-y-auto px-3" aria-label="Services">
          {services.filter((service) => service.roles.some((role) => user.roles.includes(role))).map((service) => {
            const current = service.href !== "" && (service.href === "/" ? pathname === "/" : pathname.startsWith(service.href));
            const className = `flex items-center rounded-xl py-2.5 text-sm transition duration-300 ${
              collapsed ? "justify-center px-0" : "px-3"
            } ${current ? "bg-white text-black" : "text-white/80 hover:bg-white/10 hover:text-white"}`;
            const content = (
              <>
                <ServiceIcon name={service.icon} />
                <span className={`overflow-hidden whitespace-nowrap transition-all duration-300 ${collapsed ? "w-0 opacity-0" : "ml-3 w-auto opacity-100"}`}>
                  {service.label}
                </span>
              </>
            );
            return service.href ? (
              <Link key={`${service.label}-${service.href}`} href={service.href} title={service.label} aria-current={current ? "page" : undefined} className={className}>
                {content}
              </Link>
            ) : (
              <button key={`${service.label}-${service.href}`} type="button" title={service.label} aria-label={service.label} className={className}>
                {content}
              </button>
            );
          })}
        </nav>
        <button
          type="button"
          title="Déconnexion"
          aria-label="Déconnexion"
          className={`m-3 flex items-center rounded-xl border border-white/20 py-2.5 text-sm text-white/80 transition duration-300 hover:bg-white hover:text-black ${
            collapsed ? "justify-center px-0" : "px-3"
          }`}
          onClick={() => {
            clearSession();
            router.replace("/login");
          }}
        >
          <ServiceIcon name="logout" />
          <span className={`overflow-hidden whitespace-nowrap transition-all duration-300 ${collapsed ? "w-0 opacity-0" : "ml-3 w-auto opacity-100"}`}>
            Déconnexion
          </span>
        </button>
      </aside>
      <section className="overflow-y-auto px-6 py-8 md:px-10">{children}</section>
    </div>
  );
}

function ServiceIcon({ name }: { name: (typeof services)[number]["icon"] | "logout" }) {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5 shrink-0" fill="none" stroke="currentColor" strokeWidth="1.6" aria-hidden>
      {name === "dashboard" ? (
        <>
          <rect x="4" y="4" width="7" height="7" rx="1.5" />
          <rect x="13" y="4" width="7" height="7" rx="1.5" />
          <rect x="4" y="13" width="7" height="7" rx="1.5" />
          <rect x="13" y="13" width="7" height="7" rx="1.5" />
        </>
      ) : null}
      {name === "people" ? (
        <>
          <circle cx="9" cy="8" r="2.5" />
          <path d="M4.5 18c.6-2.4 2.4-3.5 4.5-3.5s3.9 1.1 4.5 3.5" strokeLinecap="round" />
          <circle cx="16" cy="9" r="2" />
          <path d="M15.2 14.6c1.4-.3 2.8.2 3.8 1.6" strokeLinecap="round" />
        </>
      ) : null}
      {name === "contract" ? (
        <>
          <path d="M7 4.5h7l4 4V19.5H7z" strokeLinejoin="round" />
          <path d="M14 4.5V9h4.5M9.5 13h5M9.5 16h3.5" strokeLinecap="round" />
        </>
      ) : null}
      {name === "folder" ? (
        <path d="M4 8.5h6l1.5-2H20v11H4z" strokeLinejoin="round" />
      ) : null}
      {name === "calendar" ? (
        <>
          <rect x="4" y="6" width="16" height="13" rx="1.5" />
          <path d="M8 4.5V8M16 4.5V8M4 10.5h16" strokeLinecap="round" />
        </>
      ) : null}
      {name === "clock" ? (
        <>
          <circle cx="12" cy="12" r="7.5" />
          <path d="M12 8.5V12l2.5 2" strokeLinecap="round" strokeLinejoin="round" />
        </>
      ) : null}
      {name === "search" ? (
        <>
          <circle cx="11" cy="11" r="5.5" />
          <path d="M15.5 15.5 19 19" strokeLinecap="round" />
        </>
      ) : null}
      {name === "chart" ? (
        <path d="M5 18.5V14M10 18.5V9M15 18.5v-6M20 18.5V6" strokeLinecap="round" />
      ) : null}
      {name === "wallet" ? (
        <>
          <rect x="3.5" y="6" width="17" height="12" rx="2" />
          <path d="M3.5 10h17" />
          <circle cx="16.5" cy="14" r="1" fill="currentColor" stroke="none" />
        </>
      ) : null}
      {name === "settings" ? (
        <>
          <circle cx="12" cy="12" r="3" />
          <path d="M12 3.5v2.2M12 18.3V20.5M4.8 6.4l1.6 1.6M17.6 16l1.6 1.6M3.5 12h2.2M18.3 12H20.5M4.8 17.6l1.6-1.6M17.6 8l1.6-1.6" strokeLinecap="round" />
        </>
      ) : null}
      {name === "logout" ? <path d="M10 6.5H6.5v11H10M10 12h8M15 9l3 3-3 3" strokeLinecap="round" strokeLinejoin="round" /> : null}
    </svg>
  );
}
