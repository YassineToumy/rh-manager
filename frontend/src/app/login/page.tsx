"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { AuthLink, AuthShell, Field, PrimaryButton, fieldClass } from "@/components/AuthShell";
import { ApiError, api, saveSession } from "@/lib/api";
import type { LoginResponse } from "@/lib/types";

const schema = z.object({
  email: z.email("E-mail invalide"),
  password: z.string().min(1, "Mot de passe requis"),
});

type FormValues = z.infer<typeof schema>;

export default function LoginPage() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState<FormValues | null>(null);
  const [code, setCode] = useState("");
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { email: "", password: "" } });

  async function login(values: FormValues, twoFactorCode?: string) {
    setError(null);
    try {
      const session = await api<LoginResponse>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ ...values, twoFactorCode }),
      });
      if (session.requiresTwoFactor) {
        setPending(values);
        return;
      }
      saveSession(session.accessToken, session.user);
      router.push("/");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Connexion impossible.");
    }
  }

  return (
    <AuthShell title="Connexion" lede="E-mail et mot de passe du compte HRFlow.">
      {pending ? (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void login(pending, code);
          }}
        >
          <Field label="Code d'authentification" hint="Les 6 chiffres de l'application.">
            <input className={fieldClass} inputMode="numeric" autoComplete="one-time-code" value={code} onChange={(event) => setCode(event.target.value)} />
          </Field>
          {error ? <p className="auth-rise mt-4 text-sm">{error}</p> : null}
          <PrimaryButton type="submit">Vérifier</PrimaryButton>
          <button type="button" className="mt-4 block w-full text-center text-sm text-neutral-500" onClick={() => { setPending(null); setCode(""); setError(null); }}>
            Retour
          </button>
        </form>
      ) : (
      <form onSubmit={form.handleSubmit((values) => login(values))}>
        <Field label="E-mail" error={form.formState.errors.email?.message} delay={120}>
          <input type="email" className={fieldClass} autoComplete="email" {...form.register("email")} />
        </Field>
        <Field label="Mot de passe" error={form.formState.errors.password?.message} delay={190}>
          <input type="password" className={fieldClass} autoComplete="current-password" {...form.register("password")} />
        </Field>
        {error ? (
          <p className="auth-rise mt-4 text-sm" style={{ animationDelay: "40ms" }}>
            {error}
          </p>
        ) : null}
        <PrimaryButton type="submit" disabled={form.formState.isSubmitting}>
          Entrer
        </PrimaryButton>
        <AuthLink href="/register">Créer un compte</AuthLink>
      </form>
      )}
    </AuthShell>
  );
}
