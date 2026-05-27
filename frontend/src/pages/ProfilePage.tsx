import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { useAuth } from "@/auth/AuthContext";
import { LANGUAGES } from "@/api/types";

export default function ProfilePage() {
  const { t, i18n } = useTranslation();
  const { user, updateProfile, logout } = useAuth();
  const navigate = useNavigate();
  const [displayName, setDisplayName] = useState(user?.displayName ?? "");
  const [language, setLanguage] = useState(user?.preferredLanguage === "en" ? "en" : "fi");
  const [saving, setSaving] = useState(false);

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
          <span className="text-slate-600">{t("profile.yourName")}</span>
          <input
            required
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            className="border border-slate-300 rounded-lg px-3 py-2"
          />
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
          className="bg-slate-900 text-white rounded-lg py-2 px-5 text-sm font-medium hover:bg-slate-700 disabled:opacity-50"
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
