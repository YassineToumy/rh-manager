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
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { email: "", password: "" } });

  async function onSubmit(values: FormValues) {
    setError(null);
    try {
      const session = await api<LoginResponse>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify(values),
      });
      saveSession(session.accessToken, session.user);
      router.push("/");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Connexion impossible.");
    }
  }

  return (
    <AuthShell title="Connexion" lede="E-mail et mot de passe du compte HRFlow.">
      <form onSubmit={form.handleSubmit(onSubmit)}>
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
    </AuthShell>
  );
}
