"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { AuthShell, PrimaryButton } from "@/components/AuthShell";
import { clearSession, getStoredUser, getToken } from "@/lib/api";
import type { UserProfile } from "@/lib/types";

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

  return (
    <AuthShell title="Connecté" lede="La session est ouverte sur cet appareil.">
      <div className="auth-rise rounded-2xl border border-neutral-200 p-5" style={{ animationDelay: "140ms" }}>
        <p className="text-lg font-medium tracking-tight">
          {user.firstName} {user.lastName}
        </p>
        <p className="mt-1 text-sm text-neutral-500">{user.email}</p>
        <p className="mt-4 inline-block rounded-full border border-black px-3 py-1 text-xs tracking-wide">{user.roles.join(", ")}</p>
      </div>
      <PrimaryButton
        type="button"
        onClick={() => {
          clearSession();
          router.replace("/login");
        }}
      >
        Déconnexion
      </PrimaryButton>
    </AuthShell>
  );
}
