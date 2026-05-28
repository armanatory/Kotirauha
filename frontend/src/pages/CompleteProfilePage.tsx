import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAuth } from "@/auth/AuthContext";
import { LANGUAGES } from "@/api/types";

// Shown after sign-in when a new user has no name yet (e.g. signed up via the
// login form rather than the register form).
export default function CompleteProfilePage() {
  const { t } = useTranslation();
  const { user, updateProfile } = useAuth();
  const [displayName, setDisplayName] = useState("");
  const [language, setLanguage] = useState(user?.preferredLanguage === "en" ? "en" : "fi");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await updateProfile({ displayName, preferredLanguage: language });
    } catch {
      toast.error(t("auth.couldNotSave"));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center px-4">
      <form onSubmit={handleSubmit} className="w-full max-w-sm flex flex-col gap-4">
        <h1 className="text-xl font-semibold text-slate-800">{t("auth.welcomeName")}</h1>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">{t("auth.name")}</span>
          <input
            required
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            className="border border-slate-300 rounded-lg px-3 py-2"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">{t("auth.language")}</span>
          <select value={language} onChange={(e) => setLanguage(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2">
            {LANGUAGES.map((l) => (
              <option key={l.code} value={l.code}>{l.label}</option>
            ))}
          </select>
        </label>
        <button
          type="submit"
          disabled={submitting}
          className="bg-teal-700 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-teal-800 disabled:opacity-50"
        >
          {t("auth.continue")}
        </button>
      </form>
    </div>
  );
}
