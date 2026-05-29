import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { RefreshCw } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { LANGUAGES } from "@/api/types";

// Shown after sign-in when a user has no nickname yet. Neighbours only ever see
// this nickname (not a real name), so we offer an AI-generated one they can keep
// or change.
export default function CompleteProfilePage() {
  const { t, i18n } = useTranslation();
  const { user, updateProfile } = useAuth();
  const [displayName, setDisplayName] = useState("");
  const [language, setLanguage] = useState(user?.preferredLanguage === "en" ? "en" : "fi");
  const [submitting, setSubmitting] = useState(false);
  const [loadingNick, setLoadingNick] = useState(false);
  const touched = useRef(false);

  async function suggest(force = false) {
    if (touched.current && !force) return;
    setLoadingNick(true);
    try {
      const { data } = await api.get<{ nickname: string }>("/auth/suggest-nickname", {
        params: { lang: i18n.language === "en" ? "en" : "fi" },
      });
      if (data.nickname && (!touched.current || force)) setDisplayName(data.nickname);
    } catch {
      /* leave the field empty for them to type */
    } finally {
      setLoadingNick(false);
    }
  }

  useEffect(() => {
    void suggest();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
        <p className="text-sm text-slate-500">{t("auth.nicknameHint")}</p>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">{t("auth.nickname")}</span>
          <div className="flex gap-2">
            <input
              required
              value={displayName}
              onChange={(e) => { touched.current = true; setDisplayName(e.target.value); }}
              className="flex-1 border border-slate-300 rounded-lg px-3 py-2"
            />
            <button
              type="button"
              onClick={() => suggest(true)}
              disabled={loadingNick}
              title={t("auth.regenerateNickname")}
              aria-label={t("auth.regenerateNickname")}
              className="shrink-0 px-3 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-100 disabled:opacity-50"
            >
              <RefreshCw size={16} className={loadingNick ? "animate-spin" : ""} />
            </button>
          </div>
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
          disabled={submitting || !displayName.trim()}
          className="bg-teal-700 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-teal-800 disabled:opacity-50"
        >
          {t("auth.continue")}
        </button>
      </form>
    </div>
  );
}
