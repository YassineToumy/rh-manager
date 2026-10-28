"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { AuthLink, AuthShell, Field, PrimaryButton, WaitingDialog, fieldClass } from "@/components/AuthShell";
import { ChoiceList } from "@/components/ChoiceList";
import { ApiError, api } from "@/lib/api";

const roles = ["Employe", "ResponsableRH", "Manager"] as const;

const schema = z.object({
  firstName: z.string().min(1, "Prénom requis").max(80),
  lastName: z.string().min(1, "Nom requis").max(80),
  email: z.email("E-mail invalide"),
  password: z.string().min(8, "8 caractères minimum"),
  role: z
    .enum(roles, { error: "Choisissez un rôle." })
    .or(z.literal(""))
    .refine((role): role is (typeof roles)[number] => role !== "", { message: "Choisissez un rôle." }),
});

type FormInput = z.input<typeof schema>;
type FormOutput = z.output<typeof schema>;

export default function RegisterPage() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [waiting, setWaiting] = useState(false);
  const form = useForm<FormInput, unknown, FormOutput>({
    resolver: zodResolver(schema),
    defaultValues: { firstName: "", lastName: "", email: "", password: "", role: "" },
  });

  async function onSubmit(values: FormOutput) {
    setError(null);
    try {
      await api("/api/auth/register", { method: "POST", body: JSON.stringify(values) });
      setWaiting(true);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Inscription impossible.");
    }
  }

  return (
    <AuthShell title="Inscription" lede="Employé, responsable RH ou manager. Le compte reste en attente.">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <div className="grid gap-x-3 sm:grid-cols-2">
          <Field label="Prénom" error={form.formState.errors.firstName?.message} delay={120}>
            <input className={fieldClass} autoComplete="given-name" {...form.register("firstName")} />
          </Field>
          <Field label="Nom" error={form.formState.errors.lastName?.message} delay={170}>
            <input className={fieldClass} autoComplete="family-name" {...form.register("lastName")} />
          </Field>
        </div>
        <Field label="E-mail" error={form.formState.errors.email?.message} delay={220}>
          <input type="email" className={fieldClass} autoComplete="email" {...form.register("email")} />
        </Field>
        <Field
          label="Mot de passe"
          hint="8 caractères, majuscule, minuscule, chiffre et symbole."
          error={form.formState.errors.password?.message}
          delay={270}
        >
          <input type="password" className={fieldClass} autoComplete="new-password" {...form.register("password")} />
        </Field>
        <Controller
          control={form.control}
          name="role"
          render={({ field }) => (
            <ChoiceList
              label="Rôle"
              hint="Un second clic sur le rôle choisi l'annule."
              value={field.value || null}
              onChange={(role) => field.onChange(role ?? "")}
              error={form.formState.errors.role?.message}
              options={[
                { value: "Employe", label: "Employé" },
                { value: "ResponsableRH", label: "Responsable RH" },
                { value: "Manager", label: "Manager" },
              ]}
            />
          )}
        />
        {error ? <p className="auth-rise mt-4 text-sm">{error}</p> : null}
        <PrimaryButton type="submit" disabled={form.formState.isSubmitting}>
          Envoyer la demande
        </PrimaryButton>
        <AuthLink href="/login">Déjà un compte</AuthLink>
      </form>
      {waiting ? <WaitingDialog onConfirm={() => router.push("/login")} /> : null}
    </AuthShell>
  );
}
