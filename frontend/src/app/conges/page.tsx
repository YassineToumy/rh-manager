"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { DashboardFrame } from "@/components/DashboardFrame";
import { EmployeeSpace } from "@/components/EmployeeSpace";
import { getStoredUser, getToken, hasRole } from "@/lib/api";
import type { UserProfile } from "@/lib/types";

export default function LeavePage() {
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

  const employee = hasRole(user, "Employe") && !hasRole(user, "Administrateur", "ResponsableRH", "Manager");
  return (
    <DashboardFrame user={user}>
      {employee ? <EmployeeSpace section="leave" firstName={user.firstName} /> : <p className="text-sm">Cette page est réservée aux employés.</p>}
    </DashboardFrame>
  );
}
