"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { fieldClass } from "@/components/AuthShell";
import { DashboardFrame } from "@/components/DashboardFrame";
import { ApiError, api, fetchBlob, getStoredUser, getToken, updateStoredUser } from "@/lib/api";
import type { AccountSettings, TwoFactorSetup, UserProfile } from "@/lib/types";

const card = "rounded-2xl border border-neutral-200 bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,0.06)] dark:border-neutral-800 dark:bg-neutral-900";

function applyTheme(theme: "light" | "dark") {
  localStorage.setItem("hrflow_theme", theme);
  document.documentElement.classList.toggle("dark", theme === "dark");
}

export default function SettingsPage() {
  const router = useRouter();
  const [user, setUser] = useState<UserProfile | null>(null);
  const [settings, setSettings] = useState<AccountSettings | null>(null);
  const [name, setName] = useState({ firstName: "", lastName: "" });
  const [password, setPassword] = useState({ currentPassword: "", newPassword: "" });
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null);
  const [code, setCode] = useState("");
  const [disable, setDisable] = useState({ password: "", code: "" });
  const [photo, setPhoto] = useState<string | null>(null);
  const [photoStamp, setPhotoStamp] = useState(0);
  const [dark, setDark] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }
    setUser(getStoredUser());
    setDark(localStorage.getItem("hrflow_theme") === "dark");
    api<AccountSettings>("/api/auth/settings")
      .then((next) => {
        setSettings(next);
        setName({ firstName: next.firstName, lastName: next.lastName });
      })
      .catch((caught) => setError(caught instanceof ApiError ? caught.message : "Chargement impossible."));
  }, [router]);

  useEffect(() => {
    let url: string | null = null;
    fetchBlob("/api/auth/photo")
      .then((blob) => {
        if (!blob) return;
        url = URL.createObjectURL(blob);
        setPhoto(url);
      })
      .catch(() => setPhoto(null));
    return () => {
      if (url) URL.revokeObjectURL(url);
    };
  }, [photoStamp]);

  function note(message: string) {
    setError(null);
    setNotice(message);
  }

  async function saveName() {
    try {
      const profile = await api<UserProfile>("/api/auth/settings", { method: "PUT", body: JSON.stringify(name) });
      updateStoredUser(profile);
      setUser(profile);
      note("Nom modifié.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Modification impossible.");
    }
  }

  async function savePassword() {
    try {
      await api("/api/auth/change-password", { method: "POST", body: JSON.stringify(password) });
      setPassword({ currentPassword: "", newPassword: "" });
      note("Mot de passe modifié.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Modification impossible.");
    }
  }

  async function upload(file: File) {
    const body = new FormData();
    body.append("file", file);
    try {
      await api("/api/auth/photo", { method: "POST", body });
      setSettings((current) => (current ? { ...current, hasPhoto: true } : current));
      setPhotoStamp((value) => value + 1);
      window.dispatchEvent(new Event("hrflow-photo"));
      note("Photo enregistrée.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Envoi impossible.");
    }
  }

  async function removePhoto() {
    try {
      await api("/api/auth/photo", { method: "DELETE" });
      setPhoto(null);
      setSettings((current) => (current ? { ...current, hasPhoto: false } : current));
      setPhotoStamp((value) => value + 1);
      window.dispatchEvent(new Event("hrflow-photo"));
      note("Photo retirée.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Suppression impossible.");
    }
  }

  async function startTwoFactor() {
    try {
      setSetup(await api<TwoFactorSetup>("/api/auth/2fa/setup", { method: "POST" }));
      note("Scannez le code, puis saisissez les 6 chiffres.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Activation impossible.");
    }
  }

  async function enableTwoFactor() {
    try {
      await api("/api/auth/2fa/enable", { method: "POST", body: JSON.stringify({ code }) });
      setSetup(null);
      setCode("");
      setSettings((current) => (current ? { ...current, twoFactorEnabled: true } : current));
      note("Double authentification activée.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Code invalide.");
    }
  }

  async function disableTwoFactor() {
    try {
      await api("/api/auth/2fa/disable", { method: "POST", body: JSON.stringify(disable) });
      setDisable({ password: "", code: "" });
      setSettings((current) => (current ? { ...current, twoFactorEnabled: false } : current));
      note("Double authentification désactivée.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Désactivation impossible.");
    }
  }

  if (!user) return null;

  return (
    <DashboardFrame user={user}>
      <header className="auth-rise">
        <p className="text-sm text-neutral-500">Compte</p>
        <h1 className="mt-2 text-4xl font-medium tracking-tight">Paramètres</h1>
      </header>
      {error ? <p className="mt-6 rounded-2xl border border-neutral-200 bg-white p-5 text-sm dark:border-neutral-800 dark:bg-neutral-900">{error}</p> : null}
      {notice ? <p className="mt-4 text-sm">{notice}</p> : null}
      <div className="mt-8 grid gap-4 lg:grid-cols-2">
        <article className={card}>
          <h2 className="text-lg font-medium">Nom</h2>
          <label className="mt-4 block text-sm">Prénom
            <input className={fieldClass} value={name.firstName} onChange={(event) => setName({ ...name, firstName: event.target.value })} />
          </label>
          <label className="mt-3 block text-sm">Nom
            <input className={fieldClass} value={name.lastName} onChange={(event) => setName({ ...name, lastName: event.target.value })} />
          </label>
          <button type="button" className="mt-4 rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={saveName}>Enregistrer</button>
        </article>
        <article className={card}>
          <h2 className="text-lg font-medium">Photo</h2>
          <div className="mt-4 flex items-center gap-4">
            <span className="grid h-16 w-16 overflow-hidden rounded-full bg-neutral-100 text-sm dark:bg-neutral-800">
              {photo ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={photo} alt="" className="h-full w-full object-cover" />
              ) : (
                <span className="grid h-full w-full place-items-center">{user.firstName.slice(0, 1)}{user.lastName.slice(0, 1)}</span>
              )}
            </span>
            <div>
              <label className="inline-block cursor-pointer rounded-xl border border-black px-4 py-2.5 text-sm dark:border-white">
                Choisir une image
                <input type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) void upload(file);
                }} />
              </label>
              {settings?.hasPhoto ? <button type="button" className="mt-2 block text-sm text-neutral-500" onClick={removePhoto}>Retirer</button> : null}
            </div>
          </div>
          <p className="mt-3 text-sm text-neutral-500">JPEG, PNG ou WebP, 1,5 Mo maximum.</p>
        </article>
        <article className={card}>
          <h2 className="text-lg font-medium">Mot de passe</h2>
          <p className="mt-1 text-sm text-neutral-500">Il n'est jamais affiché.</p>
          <label className="mt-4 block text-sm">Mot de passe actuel
            <input type="password" className={fieldClass} value={password.currentPassword} onChange={(event) => setPassword({ ...password, currentPassword: event.target.value })} />
          </label>
          <label className="mt-3 block text-sm">Nouveau mot de passe
            <input type="password" className={fieldClass} value={password.newPassword} onChange={(event) => setPassword({ ...password, newPassword: event.target.value })} />
          </label>
          <button type="button" className="mt-4 rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={savePassword}>Changer</button>
        </article>
        <article className={card}>
          <h2 className="text-lg font-medium">Apparence</h2>
          <p className="mt-1 text-sm text-neutral-500">Le choix reste sur cet appareil.</p>
          <button
            type="button"
            role="switch"
            aria-checked={dark}
            className="mt-4 flex items-center gap-3 text-sm"
            onClick={() => {
              const next = !dark;
              setDark(next);
              applyTheme(next ? "dark" : "light");
            }}
          >
            <span className={`relative h-6 w-11 rounded-full transition ${dark ? "bg-white" : "bg-black"}`}>
              <span className={`absolute top-0.5 h-5 w-5 rounded-full transition ${dark ? "left-5 bg-black" : "left-0.5 bg-white"}`} />
            </span>
            Mode sombre
          </button>
        </article>
        <article className={`${card} lg:col-span-2`}>
          <h2 className="text-lg font-medium">Double authentification</h2>
          <p className="mt-1 text-sm text-neutral-500">Un code à 6 chiffres est demandé à chaque connexion.</p>
          {settings?.twoFactorEnabled ? (
            <div className="mt-4 grid gap-3 sm:max-w-md">
              <p className="text-sm font-medium">Activée</p>
              <label className="text-sm">Mot de passe
                <input type="password" className={fieldClass} value={disable.password} onChange={(event) => setDisable({ ...disable, password: event.target.value })} />
              </label>
              <label className="text-sm">Code actuel
                <input className={fieldClass} inputMode="numeric" value={disable.code} onChange={(event) => setDisable({ ...disable, code: event.target.value })} />
              </label>
              <button type="button" className="w-fit rounded-xl border border-black px-4 py-2.5 text-sm dark:border-white" onClick={disableTwoFactor}>Désactiver</button>
            </div>
          ) : (
            <div className="mt-4">
              <button type="button" className="rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={startTwoFactor}>Activer</button>
              {setup ? (
                <div className="mt-4 flex flex-col gap-4 sm:flex-row sm:items-start">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={setup.qrCodeDataUrl} alt="QR code de l'authentificateur" className="h-40 w-40 rounded-xl bg-white p-2" />
                  <div className="sm:max-w-sm">
                    <p className="text-sm text-neutral-500">Ou saisissez cette clé dans l'application.</p>
                    <p className="mt-2 font-mono text-sm tracking-wide">{setup.sharedKey}</p>
                    <label className="mt-3 block text-sm">Code de vérification
                      <input className={fieldClass} inputMode="numeric" value={code} onChange={(event) => setCode(event.target.value)} />
                    </label>
                    <button type="button" className="mt-4 rounded-xl bg-black px-4 py-2.5 text-sm text-white" onClick={enableTwoFactor}>Confirmer</button>
                  </div>
                </div>
              ) : null}
            </div>
          )}
        </article>
      </div>
    </DashboardFrame>
  );
}
