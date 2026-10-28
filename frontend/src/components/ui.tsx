import type { ButtonHTMLAttributes, ReactNode } from "react";

export function PageTitle({ title, text }: { title: string; text?: string }) {
  return (
    <header className="mb-6">
      <h1 className="text-3xl tracking-tight">{title}</h1>
      {text ? <p className="mt-1 max-w-2xl text-sm text-[#5c6570]">{text}</p> : null}
    </header>
  );
}

export function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <section className={`rounded-2xl border border-[#e4dccb] bg-white p-5 shadow-sm ${className}`}>{children}</section>;
}

export function Button({
  children,
  tone = "primary",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { tone?: "primary" | "ghost" | "danger" }) {
  const tones = {
    primary: "bg-[#0f6e6e] text-white hover:bg-[#0c5a5a]",
    ghost: "border border-[#d9d0c1] bg-white hover:bg-[#f7f3ea]",
    danger: "bg-[#a34b32] text-white hover:bg-[#8a3e2a]",
  };
  return (
    <button
      {...props}
      className={`inline-flex items-center justify-center rounded-full px-4 py-2 text-sm font-medium disabled:opacity-50 ${tones[tone]} ${props.className ?? ""}`}
    >
      {children}
    </button>
  );
}

export function Field({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium">{label}</span>
      {children}
      {error ? <span className="mt-1 block text-xs text-[#a34b32]">{error}</span> : null}
    </label>
  );
}

export const inputClass = "w-full rounded-xl border border-[#d9d0c1] bg-[#fffcf7] px-3 py-2 text-sm outline-none focus:border-[#0f6e6e]";

export function Badge({ value }: { value: string }) {
  const tone =
    value === "Approved" || value === "Active" || value === "Present" || value === "Published" || value === "Done" || value === "Accepted"
      ? "bg-[#e5f4ef] text-[#0f6e6e]"
      : value === "Rejected" || value === "Late" || value === "Expired" || value === "Archived" || value === "Terminated"
        ? "bg-[#f8e8e3] text-[#a34b32]"
        : "bg-[#f3efe6] text-[#5c6570]";
  const labels: Record<string, string> = {
    Active: "Actif",
    Archived: "Archivé",
    Pending: "En attente",
    Approved: "Acceptée",
    Rejected: "Refusée",
    Present: "Présent",
    Late: "Retard",
    Absent: "Absent",
    Draft: "Brouillon",
    Published: "Publiée",
    Closed: "Clôturée",
    Expired: "Expiré",
    Terminated: "Rompu",
    Received: "Reçue",
    InReview: "En étude",
    Interview: "Entretien",
    Accepted: "Acceptée",
    Open: "Ouvert",
    InProgress: "En cours",
    Done: "Atteint",
    Cancelled: "Annulé",
  };
  return <span className={`rounded-full px-2 py-1 text-xs font-medium ${tone}`}>{labels[value] ?? value}</span>;
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="rounded-xl border border-dashed border-[#d9d0c1] px-4 py-6 text-sm text-[#5c6570]">{children}</p>;
}

export function ErrorNote({ message }: { message: string | null }) {
  if (!message) return null;
  return <p className="rounded-xl bg-[#f8e8e3] px-3 py-2 text-sm text-[#a34b32]">{message}</p>;
}
