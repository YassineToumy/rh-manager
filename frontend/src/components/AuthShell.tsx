import type { ButtonHTMLAttributes, ReactNode } from "react";
import Link from "next/link";

export const fieldClass =
  "mt-2 w-full rounded-xl border border-neutral-200 bg-white px-3.5 py-3 text-base text-black shadow-[0_8px_24px_rgba(0,0,0,0.06)] outline-none transition duration-300 ease-out placeholder:text-neutral-400 hover:shadow-[0_10px_28px_rgba(0,0,0,0.1)] focus:border-neutral-400 focus:shadow-[0_12px_32px_rgba(0,0,0,0.12)] focus:ring-4 focus:ring-black/5";

export function AuthShell({
  title,
  lede,
  children,
}: {
  title: string;
  lede: string;
  children: ReactNode;
}) {
  return (
    <main className="grid h-screen overflow-hidden bg-white text-black md:grid-cols-2">
      <aside className="relative hidden overflow-hidden bg-black text-white md:flex md:flex-col md:justify-between md:p-10">
        <div className="auth-grid pointer-events-none absolute inset-0" />
        <div className="auth-orb auth-orb-a pointer-events-none absolute" />
        <div className="auth-orb auth-orb-b pointer-events-none absolute" />
        <p className="auth-rise relative text-sm font-medium tracking-[0.22em]">HRFLOW</p>
        <div className="relative max-w-md">
          <p className="auth-rise text-4xl leading-[1.05] font-medium tracking-tight" style={{ animationDelay: "80ms" }}>
            L'accès, clairement.
          </p>
          <p className="auth-rise mt-5 text-base leading-7 text-white/70" style={{ animationDelay: "160ms" }}>
            Employés, responsables RH et managers demandent un compte. Un administrateur valide ensuite.
          </p>
        </div>
        <div className="auth-rise relative flex flex-wrap gap-x-6 gap-y-2 text-sm text-white/60" style={{ animationDelay: "240ms" }}>
          <span>Connexion</span>
          <span>Liste d'attente</span>
          <span>Validation</span>
        </div>
      </aside>
      <section className="flex justify-center overflow-y-auto px-6 py-10">
        <div className="my-auto w-full max-w-md">
          <h1 className="auth-rise text-3xl font-medium tracking-tight">{title}</h1>
          <p className="auth-rise mt-2 text-sm leading-6 text-neutral-500" style={{ animationDelay: "70ms" }}>
            {lede}
          </p>
          <div className="mt-8">{children}</div>
        </div>
      </section>
    </main>
  );
}

export function Field({
  label,
  hint,
  error,
  delay = 0,
  children,
}: {
  label: string;
  hint?: string;
  error?: string;
  delay?: number;
  children: ReactNode;
}) {
  return (
    <label className="auth-rise mt-4 block text-sm font-medium" style={{ animationDelay: `${delay}ms` }}>
      {label}
      {children}
      {hint ? <span className="mt-1.5 block text-xs font-normal text-neutral-500">{hint}</span> : null}
      {error ? <span className="mt-1.5 block text-xs font-normal">{error}</span> : null}
    </label>
  );
}

export function PrimaryButton({ children, className = "", ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      className={`auth-button relative mt-6 w-full overflow-hidden rounded-xl bg-black py-3 text-sm font-medium text-white transition duration-300 ease-out hover:-translate-y-0.5 hover:shadow-[0_12px_30px_rgba(0,0,0,0.18)] active:translate-y-0 disabled:translate-y-0 disabled:opacity-50 ${className}`}
    >
      <span className="relative">{children}</span>
    </button>
  );
}

export function AuthLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <Link href={href} className="auth-rise group mt-5 block text-center text-sm text-neutral-500" style={{ animationDelay: "420ms" }}>
      <span className="border-b border-transparent pb-0.5 transition duration-300 group-hover:border-black group-hover:text-black">
        {children}
      </span>
    </Link>
  );
}

export function WaitingDialog({ onConfirm }: { onConfirm: () => void }) {
  return (
    <div className="auth-backdrop fixed inset-0 z-20 flex items-center justify-center bg-black/50 px-4 backdrop-blur-sm" role="dialog" aria-modal="true">
      <div className="auth-dialog w-full max-w-sm rounded-2xl border border-neutral-200 bg-white p-8 text-center shadow-[0_24px_80px_rgba(0,0,0,0.18)]">
        <p className="text-base leading-7 font-medium">wait until admin approves your request</p>
        <PrimaryButton type="button" onClick={onConfirm}>
          OK
        </PrimaryButton>
      </div>
    </div>
  );
}
