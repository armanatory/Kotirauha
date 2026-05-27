import { useTranslation } from "react-i18next";
import { useAuth } from "@/auth/AuthContext";

export default function ProfilePage() {
  const { user } = useAuth();
  const { i18n } = useTranslation();

  function changeLang(lang: string) {
    void i18n.changeLanguage(lang);
    localStorage.setItem("lang", lang);
  }

  return (
    <div className="max-w-md space-y-4">
      <h1 className="text-xl font-semibold text-slate-800">Profile</h1>
      <div className="bg-white border border-slate-200 rounded-xl p-4 text-sm space-y-1">
        <p><span className="text-slate-500">Name:</span> {user?.displayName}</p>
        <p><span className="text-slate-500">Email:</span> {user?.email}</p>
        {user?.membership && (
          <p><span className="text-slate-500">Building:</span> {user.membership.buildingName} ({user.membership.role})</p>
        )}
      </div>

      <label className="flex flex-col gap-1 text-sm max-w-xs">
        <span className="text-slate-600">Interface language</span>
        <select
          defaultValue={i18n.language}
          onChange={(e) => changeLang(e.target.value)}
          className="border border-slate-300 rounded-lg px-3 py-2"
        >
          <option value="en">English</option>
          <option value="fi">Suomi</option>
        </select>
      </label>
    </div>
  );
}
