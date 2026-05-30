import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { RefreshCw } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { LANGUAGES } from "@/api/types";

export default function ProfilePage() {
  const { t, i18n } = useTranslation();
  const { user, updateProfile, logout } = useAuth();
  const navigate = useNavigate();
  const [displayName, setDisplayName] = useState(user?.displayName ?? "");
  const [language, setLanguage] = useState(user?.preferredLanguage === "en" ? "en" : "fi");
  const [saving, setSaving] = useState(false);
  const [suggestingNick, setSuggestingNick] = useState(false);

  async function suggestNickname() {
    setSuggestingNick(true);
    try {
      const { data } = await api.get<{ nickname: string }>("/auth/suggest-nickname", {
        params: { lang: language },
      });
      if (data.nickname) setDisplayName(data.nickname);
    } catch {
      /* leave the field as-is */
    } finally {
      setSuggestingNick(false);
    }
  }

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await updateProfile({ displayName, preferredLanguage: language });
      void i18n.changeLanguage(language);
      localStorage.setItem("lang", language);
      toast.success(t("profile.saved"));
    } catch {
      toast.error(t("profile.couldNotSave"));
    } finally {
      setSaving(false);
    }
  }

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <div className="max-w-md space-y-5">
      <h1 className="text-xl font-semibold text-slate-800">{t("profile.title")}</h1>

      <div className="bg-white border border-slate-200 rounded-xl p-4 text-sm space-y-1">
        <p><span className="text-slate-500">{t("profile.email")}:</span> {user?.email}</p>
        {user?.membership && (
          <p>
            <span className="text-slate-500">{t("profile.building")}:</span> {user.membership.buildingName} ({t(`roles.${user.membership.role}`, user.membership.role)})
          </p>
        )}
      </div>

      <form onSubmit={save} className="bg-white border border-slate-200 rounded-xl p-4 space-y-3">
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">{t("profile.yourNickname")}</span>
          <div className="flex gap-2">
            <input
              required
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              className="flex-1 border border-slate-300 rounded-lg px-3 py-2"
            />
            <button
              type="button"
              onClick={suggestNickname}
              disabled={suggestingNick}
              title={t("profile.suggestNickname")}
              aria-label={t("profile.suggestNickname")}
              className="shrink-0 px-3 rounded-lg border border-slate-300 text-slate-600 hover:bg-slate-100 disabled:opacity-50"
            >
              <RefreshCw size={16} className={suggestingNick ? "animate-spin" : ""} />
            </button>
          </div>
          <p className="text-xs text-slate-500">{t("profile.nicknameHint")}</p>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">{t("profile.language")}</span>
          <select value={language} onChange={(e) => setLanguage(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2">
            {LANGUAGES.map((l) => (
              <option key={l.code} value={l.code}>{l.label}</option>
            ))}
          </select>
        </label>
        <button
          type="submit"
          disabled={saving}
          className="bg-teal-700 text-white rounded-lg py-2 px-5 text-sm font-medium hover:bg-teal-800 disabled:opacity-50"
        >
          {t("profile.save")}
        </button>
      </form>

      <button onClick={handleLogout} className="text-sm text-slate-500 underline">
        {t("profile.logout")}
      </button>
    </div>
  );
}
